using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Servicos;

public sealed class ServicoConfiguracao(
    IRepositorioConfiguracao configuracoes,
    IRepositorioPosicoesLeitura posicoesLeitura,
    IServicoGeolocalizacao geolocalizacao,
    ServicoProtecao protecao,
    ServicoAuditoria auditoria,
    IContextoUsuario contexto,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    public Task<Configuracao> ObterAsync(CancellationToken ct = default) => configuracoes.ObterAsync(ct);

    public StatusBaseGeo ObterStatusGeo() => geolocalizacao.ObterStatus();

    /// <summary>
    /// Arquivos e canais que o Worker acompanha, com a posicao lida e a hora da ultima leitura.
    /// A chave gravada pelo Worker tem o formato "Fonte:origem".
    /// </summary>
    public async Task<IReadOnlyList<LeituraFonte>> ListarLeiturasAsync(CancellationToken ct = default) =>
        (await posicoesLeitura.ListarAsync(ct))
            .Select(p =>
            {
                var separador = p.Chave.IndexOf(':');
                var fonte = separador > 0 && Enum.TryParse<TipoFonte>(p.Chave[..separador], out var tipo) ? tipo : (TipoFonte?)null;
                return new LeituraFonte(fonte, separador > 0 ? p.Chave[(separador + 1)..] : p.Chave, p.Posicao, p.AtualizadaEm);
            })
            .ToList();

    public async Task<Resultado> SalvarMotorAsync(DadosConfiguracaoMotor dados, CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        var simulacaoAnterior = configuracao.ModoSimulacao;
        try
        {
            configuracao.AlterarMotor(dados.ModoSimulacao, dados.TemposBloqueio, dados.JanelaReincidenciaDias,
                dados.MonitorarLogIis, dados.CaminhoLogIis, dados.MonitorarHttpErr, dados.CaminhoHttpErr,
                dados.MonitorarEventosWindows, dados.RetencaoEventosDias, relogio.GetUtcNow().UtcDateTime, contexto.Nome);
        }
        catch (ArgumentException ex)
        {
            return Resultado.Falha(ex.Message);
        }

        auditoria.Registrar("Configurações alteradas", "Motor",
            simulacaoAnterior != dados.ModoSimulacao ? $"Modo simulação: {dados.ModoSimulacao}" : null);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> SalvarPoliticaPaisesAsync(DadosPoliticaPaises dados, CancellationToken ct = default)
    {
        if (dados.Modo != ModoPoliticaPaises.Desativada && contexto.Ip is not null && EnderecoIp.TentarConverter(contexto.Ip, out var ipUsuario))
        {
            var protegido = (await protecao.ObterAsync(ct)).Contem(ipUsuario);
            var paisUsuario = geolocalizacao.Localizar(ipUsuario)?.PaisCodigo;
            var paises = dados.Paises.Select(p => p.ToUpperInvariant()).ToHashSet();
            var listado = paisUsuario is not null && paises.Contains(paisUsuario);
            var ficariaBloqueado = paisUsuario is not null &&
                (dados.Modo == ModoPoliticaPaises.PermitirSomenteListados ? !listado : listado);

            if (ficariaBloqueado && !protegido)
                return Resultado.Falha("Seu IP atual ficaria bloqueado por esta política. Inclua seu país ou adicione seu IP à lista branca.");
        }

        var configuracao = await configuracoes.ObterAsync(ct);
        try
        {
            configuracao.AlterarPoliticaPaises(dados.Modo, dados.Aplicacao, dados.Paises, dados.Portas,
                relogio.GetUtcNow().UtcDateTime, contexto.Nome);
        }
        catch (ArgumentException ex)
        {
            return Resultado.Falha(ex.Message);
        }

        auditoria.Registrar("Política de países alterada", dados.Modo.ToString(),
            $"{dados.Aplicacao} | {configuracao.PaisesPolitica} | {configuracao.PortasPolitica}");
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
