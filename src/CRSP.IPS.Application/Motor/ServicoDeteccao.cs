using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Domain.Politicas;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Recebe eventos normalizados das fontes, aplica protecao, politica de paises (modo reativo), listas externas
/// em modo Reativa e regras, e registra bloqueios com punicao progressiva.
/// </summary>
public sealed class ServicoDeteccao(
    IRepositorioConfiguracao configuracoes,
    IRepositorioRegras regras,
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IServicoGeolocalizacao geolocalizacao,
    ServicoProtecao protecao,
    AvaliadorRegras avaliador,
    ContadorJanelaDeslizante contador,
    LimitadorAmostras limitadorAmostras,
    IRepositorioListasExternas listasExternas,
    MapaListasExternas mapaListasExternas,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoDeteccao> logger)
{
    public const string Sistema = "CRSPIPS";

    public async Task<int> ProcessarAsync(IReadOnlyList<EventoDetectado> lote, CancellationToken ct = default)
    {
        if (lote.Count == 0)
            return 0;

        var agora = relogio.GetUtcNow().UtcDateTime;
        var configuracao = await configuracoes.ObterAsync(ct);
        var regrasAtivas = (await regras.ListarAsync(ct)).Where(r => r.Ativa).ToList();
        var conjuntoProtecao = await protecao.ObterAsync(ct);
        var ativos = await bloqueios.ListarAtivosAsync(ct);
        var ipsBloqueadosReais = ativos.Where(b => !b.Simulado).Select(b => b.Ip).ToHashSet();
        var ipsBloqueadosSimulados = ativos.Where(b => b.Simulado).Select(b => b.Ip).ToHashSet();
        var politicaAtiva = configuracao.ModoPaises != ModoPoliticaPaises.Desativada;
        var politicaTodoTrafego = politicaAtiva && configuracao.AplicacaoPaises == AplicacaoPoliticaPaises.Reativa;
        var politicaSuspeitos = politicaAtiva && configuracao.AplicacaoPaises == AplicacaoPoliticaPaises.ReativaSuspeitos;

        await mapaListasExternas.AtualizarSeNecessarioAsync(listasExternas, ct);

        var novosBloqueios = new Dictionary<string, Bloqueio>();
        var amostras = new List<EventoSeguranca>();
        var localizacoes = new Dictionary<string, LocalizacaoIp?>();
        var coincidencias = new Dictionary<(int ListaId, string Ip), (int Quantidade, EventoDetectado Ultimo)>();

        LocalizacaoIp? Localizar(EnderecoIp ip)
        {
            var chave = ip.ToString();
            if (!localizacoes.TryGetValue(chave, out var local))
            {
                local = geolocalizacao.Localizar(ip);
                localizacoes[chave] = local;
            }

            return local;
        }

        foreach (var evento in lote.OrderBy(e => e.OcorridoEmUtc))
        {
            var ip = evento.Ip.ToString();
            var listasDoIp = mapaListasExternas.ListasQueContem(evento.Ip);
            foreach (var listaId in listasDoIp)
            {
                var atual = coincidencias.GetValueOrDefault((listaId, ip));
                coincidencias[(listaId, ip)] = (atual.Quantidade + 1, evento);
            }
            var listaReativa = mapaListasExternas.ObterListaReativa(listasDoIp);

            if (ipsBloqueadosReais.Contains(ip) || conjuntoProtecao.Contem(evento.Ip))
                continue;

            // IP ja bloqueado em simulacao: o atacante continua chegando, entao os eventos seguem sendo registrados
            // para as estatisticas, mas nenhum novo bloqueio e criado.
            var jaBloqueadoSimulado = ipsBloqueadosSimulados.Contains(ip) || novosBloqueios.ContainsKey(ip);

            if (politicaTodoTrafego && !jaBloqueadoSimulado)
            {
                var local = Localizar(evento.Ip);
                if (!configuracao.PaisPermitido(local?.PaisCodigo))
                {
                    amostras.Add(CriarAmostra(evento, null, local));
                    novosBloqueios[ip] = await CriarBloqueioAsync(evento.Ip, configuracao, OrigemBloqueio.Pais,
                        $"País não permitido: {local?.PaisCodigo}", null, 1, local, agora, ct);
                    continue;
                }
            }

            foreach (var regra in regrasAtivas)
            {
                if (!avaliador.Corresponde(regra, evento))
                    continue;

                var local = Localizar(evento.Ip);
                if (limitadorAmostras.DeveRegistrar(regra.Id, ip, evento.OcorridoEmUtc))
                    amostras.Add(CriarAmostra(evento, regra.Id, local));

                if (jaBloqueadoSimulado)
                    break;

                if (politicaSuspeitos && !configuracao.PaisPermitido(local?.PaisCodigo))
                {
                    novosBloqueios[ip] = await CriarBloqueioAsync(evento.Ip, configuracao, OrigemBloqueio.Pais,
                        $"País não permitido: {local?.PaisCodigo}", regra.Id, 1, local, agora, ct);
                    break;
                }

                // Lista externa em modo Reativa: IP ja conhecido como malicioso nao ganha o limite da regra.
                if (listaReativa is not null)
                {
                    novosBloqueios[ip] = await CriarBloqueioAsync(evento.Ip, configuracao, OrigemBloqueio.ListaExterna,
                        $"Lista externa: {listaReativa}", regra.Id, 1, local, agora, ct);
                    break;
                }

                var ocorrencias = contador.Registrar(regra.Id, ip, evento.OcorridoEmUtc, regra.Janela);
                if (ocorrencias < regra.LimiteOcorrencias)
                    continue;

                contador.Zerar(regra.Id, ip);
                novosBloqueios[ip] = await CriarBloqueioAsync(evento.Ip, configuracao, OrigemBloqueio.Automatico,
                    regra.Nome, regra.Id, ocorrencias, local, agora, ct);
                break;
            }
        }

        if (amostras.Count > 0)
            eventos.AdicionarVarios(amostras);
        foreach (var bloqueio in novosBloqueios.Values)
            bloqueios.Adicionar(bloqueio);
        if (coincidencias.Count > 0)
            await RegistrarCoincidenciasAsync(coincidencias, ct);

        if (amostras.Count > 0 || novosBloqueios.Count > 0 || coincidencias.Count > 0)
            await unidadeDeTrabalho.SalvarAsync(ct);

        foreach (var bloqueio in novosBloqueios.Values)
        {
            logger.LogInformation(
                "IP {Ip} bloqueado. Motivo {Motivo}, nivel {Nivel}, expira {ExpiraEm}, simulado {Simulado}",
                bloqueio.Ip, bloqueio.Motivo, bloqueio.NivelReincidencia, bloqueio.ExpiraEm, bloqueio.Simulado);
        }

        return novosBloqueios.Count;
    }

    /// <summary>Agrega por (lista, IP): um registro por IP com a quantidade e a ultima requisicao vista.</summary>
    private async Task RegistrarCoincidenciasAsync(
        Dictionary<(int ListaId, string Ip), (int Quantidade, EventoDetectado Ultimo)> coincidencias, CancellationToken ct)
    {
        var existentes = (await listasExternas.ObterCoincidenciasAsync(coincidencias.Keys.ToList(), ct))
            .ToDictionary(c => (c.ListaExternaId, c.Ip));

        foreach (var ((listaId, ip), (quantidade, ultimo)) in coincidencias)
        {
            if (!existentes.TryGetValue((listaId, ip), out var registro))
            {
                registro = CoincidenciaListaExterna.Criar(listaId, ip, ultimo.OcorridoEmUtc);
                listasExternas.AdicionarCoincidencia(registro);
            }

            registro.Registrar(quantidade, ultimo.OcorridoEmUtc, ultimo.Fonte, ultimo.Site, ultimo.Url, ultimo.CodigoStatus);
        }
    }

    private async Task<Bloqueio> CriarBloqueioAsync(
        EnderecoIp ip,
        Configuracao configuracao,
        OrigemBloqueio origem,
        string motivo,
        int? regraId,
        int ocorrencias,
        LocalizacaoIp? local,
        DateTime agora,
        CancellationToken ct)
    {
        var anteriores = await bloqueios.ContarPorIpDesdeAsync(ip.ToString(), agora.AddDays(-configuracao.JanelaReincidenciaDias), ct);
        var (nivel, duracao) = PoliticaProgressao.Calcular(configuracao.ObterTemposBloqueio(), anteriores);

        return Bloqueio.Criar(ip, origem, motivo, agora, duracao, nivel, configuracao.ModoSimulacao, Sistema,
            ocorrencias, regraId, localizacao: local);
    }

    private static EventoSeguranca CriarAmostra(EventoDetectado evento, int? regraId, LocalizacaoIp? local) =>
        EventoSeguranca.Criar(
            evento.Ip.ToString(),
            evento.Fonte,
            regraId,
            evento.OcorridoEmUtc,
            evento.Metodo,
            evento.Url,
            evento.CodigoStatus,
            evento.UserAgent,
            evento.MotivoHttpErr ?? evento.Detalhe,
            local?.PaisCodigo,
            evento.Site);
}
