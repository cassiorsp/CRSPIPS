using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;

namespace CRSP.IPS.Application.Servicos;

/// <summary>
/// Credenciais MaxMind no painel. O painel apenas grava a chave (protegida) e sinaliza a atualizacao;
/// quem baixa as bases e o Worker, que tem permissao de escrita na pasta GeoIP.
/// </summary>
public sealed class ServicoGeoIp(
    IRepositorioConfiguracaoGeoIp configuracoes,
    IProtetorSegredos protetor,
    ServicoAuditoria auditoria,
    IUnidadeDeTrabalho unidadeDeTrabalho)
{
    public Task<ConfiguracaoGeoIp> ObterAsync(CancellationToken ct = default) => configuracoes.ObterAsync(ct);

    public async Task<Resultado> SalvarCredenciaisAsync(string contaId, string? chaveLicenca, bool atualizacaoAutomatica, CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        var chave = chaveLicenca?.Trim();
        try
        {
            configuracao.AlterarCredenciais(
                contaId,
                string.IsNullOrEmpty(chave) ? null : protetor.Proteger(chave),
                string.IsNullOrEmpty(chave) ? null : chave[^Math.Min(4, chave.Length)..],
                atualizacaoAutomatica);
        }
        catch (ArgumentException ex)
        {
            return Resultado.Falha(ex.Message);
        }

        auditoria.Registrar("Credenciais MaxMind alteradas", $"Conta {configuracao.ContaId}");
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> SolicitarAtualizacaoAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        try
        {
            configuracao.SolicitarAtualizacao();
        }
        catch (InvalidOperationException ex)
        {
            return Resultado.Falha(ex.Message);
        }

        auditoria.Registrar("Atualização GeoIP solicitada", "MaxMind");
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> RemoverCredenciaisAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        configuracao.RemoverCredenciais();
        auditoria.Registrar("Credenciais MaxMind removidas", "MaxMind");
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
