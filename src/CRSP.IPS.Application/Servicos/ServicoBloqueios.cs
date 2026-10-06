using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Servicos;

/// <summary>
/// Operacoes manuais sobre bloqueios. Altera apenas o estado no banco; o Worker aplica no firewall em ate 2 segundos.
/// </summary>
public sealed class ServicoBloqueios(
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IRepositorioRegras regras,
    IRepositorioListas listas,
    IRepositorioListasExternas listasExternas,
    IServicoGeolocalizacao geolocalizacao,
    ServicoProtecao protecao,
    ServicoAuditoria auditoria,
    IContextoUsuario contexto,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoBloqueios> logger)
{
    public Task<Fatia<Bloqueio>> PesquisarAsync(FiltroBloqueios filtro, CancellationToken ct = default) =>
        bloqueios.PesquisarAsync(filtro with
        {
            Pular = Math.Max(0, filtro.Pular),
            Quantidade = Math.Clamp(filtro.Quantidade, 1, 200)
        }, ct);

    public const int LimiteExportacao = 50_000;

    /// <summary>Todos os bloqueios do filtro (sem a rolagem da tela), limitados a <see cref="LimiteExportacao"/>.</summary>
    public async Task<IReadOnlyList<Bloqueio>> ListarParaExportacaoAsync(FiltroBloqueios filtro, CancellationToken ct = default) =>
        (await bloqueios.PesquisarAsync(filtro with { Pular = 0, Quantidade = LimiteExportacao }, ct)).Itens;

    public async Task<Resultado<DetalheIp>> ObterDetalheAsync(string ipTexto, CancellationToken ct = default)
    {
        if (!EnderecoIp.TentarConverter(ipTexto, out var ip))
            return Resultado<DetalheIp>.Falha("Endereço IP inválido.");

        var texto = ip.ToString();
        var historico = await bloqueios.ListarPorIpAsync(texto, ct);
        var nomesRegras = (await regras.ListarAsync(ct)).ToDictionary(r => r.Id, r => r.Nome);
        var amostras = (await eventos.ListarPorIpAsync(texto, 200, ct))
            .Select(e => MapearEvento(e, nomesRegras))
            .ToList();
        var conjuntoProtecao = await protecao.ObterAsync(ct);
        var listaNegra = (await listas.ListarAsync(TipoLista.Negra, ct)).Any(e => e.ObterFaixa().Contem(ip));

        return Resultado<DetalheIp>.Ok(new DetalheIp(
            texto,
            geolocalizacao.Localizar(ip),
            conjuntoProtecao.Contem(ip),
            conjuntoProtecao.ObterMotivo(ip),
            listaNegra,
            await listasExternas.ListarQueContemAsync(ip.ObterChaveOrdenavel(), ct),
            historico.FirstOrDefault(b => b.Status == StatusBloqueio.Ativo),
            historico,
            amostras));
    }

    public async Task<Resultado> BloquearManualAsync(string ipTexto, TimeSpan? duracao, string? comentario, CancellationToken ct = default)
    {
        if (!EnderecoIp.TentarConverter(ipTexto, out var ip))
            return Resultado.Falha("Endereço IP inválido.");

        if (contexto.Ip is not null && EnderecoIp.TentarConverter(contexto.Ip, out var ipUsuario) && ipUsuario.Equals(ip))
            return Resultado.Falha("Você não pode bloquear o seu próprio IP.");

        var motivoProtecao = (await protecao.ObterAsync(ct)).ObterMotivo(ip);
        if (motivoProtecao is not null)
            return Resultado.Falha("O IP está protegido (lista branca, servidor ou administrador) e não pode ser bloqueado.");

        var agora = relogio.GetUtcNow().UtcDateTime;
        var existente = await bloqueios.ObterAtivoPorIpAsync(ip.ToString(), ct);
        if (existente is not null)
        {
            if (!existente.Simulado)
                return Resultado.Falha("Este IP já está bloqueado.");
            existente.Liberar(agora, contexto.Nome, "Substituído por bloqueio manual");
        }

        var bloqueio = Bloqueio.Criar(
            ip,
            OrigemBloqueio.Manual,
            "Bloqueio manual",
            agora,
            duracao,
            nivelReincidencia: 1,
            simulado: false,
            criadoPor: contexto.Nome,
            comentario: comentario,
            localizacao: geolocalizacao.Localizar(ip));
        bloqueios.Adicionar(bloqueio);

        auditoria.Registrar("Bloqueio manual", ip.ToString(),
            $"{(duracao.HasValue ? DuracaoTexto.Formatar(duracao.Value) : "permanente")} {comentario}".Trim());
        await unidadeDeTrabalho.SalvarAsync(ct);

        logger.LogInformation("Bloqueio manual do IP {Ip} por {Usuario}", ip, contexto.Nome);
        return Resultado.Ok();
    }

    public async Task<Resultado> DesbloquearAsync(int bloqueioId, string? comentario, CancellationToken ct = default)
    {
        var bloqueio = await bloqueios.ObterPorIdAsync(bloqueioId, ct);
        if (bloqueio is null)
            return Resultado.Falha("Bloqueio não encontrado.");
        if (bloqueio.Status != StatusBloqueio.Ativo)
            return Resultado.Falha("Somente bloqueios ativos podem ser liberados.");

        bloqueio.Liberar(relogio.GetUtcNow().UtcDateTime, contexto.Nome, comentario);
        auditoria.Registrar("Desbloqueio", bloqueio.Ip, comentario);
        await unidadeDeTrabalho.SalvarAsync(ct);

        logger.LogInformation("IP {Ip} desbloqueado por {Usuario}", bloqueio.Ip, contexto.Nome);
        return Resultado.Ok();
    }

    public async Task<Resultado> DesbloquearVariosAsync(IReadOnlyCollection<int> ids, string? comentario, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var liberados = 0;
        foreach (var id in ids.Distinct())
        {
            var bloqueio = await bloqueios.ObterPorIdAsync(id, ct);
            if (bloqueio is not { Status: StatusBloqueio.Ativo })
                continue;
            bloqueio.Liberar(agora, contexto.Nome, comentario);
            auditoria.Registrar("Desbloqueio", bloqueio.Ip, comentario);
            liberados++;
        }

        if (liberados == 0)
            return Resultado.Falha("Nenhum bloqueio ativo selecionado.");

        await unidadeDeTrabalho.SalvarAsync(ct);
        logger.LogInformation("{Quantidade} IPs desbloqueados em lote por {Usuario}", liberados, contexto.Nome);
        return Resultado.Ok();
    }

    public async Task<Resultado> AlterarExpiracaoAsync(int bloqueioId, TimeSpan? novaDuracaoAPartirDeAgora, CancellationToken ct = default)
    {
        var bloqueio = await bloqueios.ObterPorIdAsync(bloqueioId, ct);
        if (bloqueio is null)
            return Resultado.Falha("Bloqueio não encontrado.");

        var agora = relogio.GetUtcNow().UtcDateTime;
        try
        {
            bloqueio.AlterarExpiracao(novaDuracaoAPartirDeAgora.HasValue ? agora.Add(novaDuracaoAPartirDeAgora.Value) : null, agora);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Resultado.Falha("Somente bloqueios ativos podem ter a expiração alterada.");
        }

        auditoria.Registrar("Expiração alterada", bloqueio.Ip,
            novaDuracaoAPartirDeAgora.HasValue ? DuracaoTexto.Formatar(novaDuracaoAPartirDeAgora.Value) : "permanente");
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    internal static EventoResumo MapearEvento(EventoSeguranca evento, IReadOnlyDictionary<int, string> nomesRegras) => new(
        evento.Id,
        evento.Ip,
        evento.Fonte,
        evento.RegraId is { } regraId && nomesRegras.TryGetValue(regraId, out var nome) ? nome : null,
        evento.OcorridoEm,
        evento.Metodo,
        evento.Url,
        evento.CodigoStatus,
        evento.UserAgent,
        evento.Detalhe,
        evento.PaisCodigo,
        evento.Site);
}
