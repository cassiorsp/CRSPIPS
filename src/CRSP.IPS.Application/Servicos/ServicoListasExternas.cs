using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;

namespace CRSP.IPS.Application.Servicos;

/// <summary>
/// Operacoes do painel sobre listas externas. O painel apenas liga/desliga e pede atualizacao;
/// o download e a aplicacao no firewall sao feitos pelo Worker.
/// </summary>
public sealed class ServicoListasExternas(
    IRepositorioListasExternas listas,
    IRepositorioConfiguracao configuracoes,
    ServicoAuditoria auditoria,
    IUnidadeDeTrabalho unidadeDeTrabalho)
{
    public Task<IReadOnlyList<ListaExterna>> ListarAsync(CancellationToken ct = default) => listas.ListarAsync(ct);

    public Task<IReadOnlyList<CoincidenciaListaExterna>> ListarCoincidenciasAsync(CancellationToken ct = default) =>
        listas.ListarCoincidenciasRecentesAsync(100, ct);

    public async Task<bool> EstaEmSimulacaoAsync(CancellationToken ct = default) => (await configuracoes.ObterAsync(ct)).ModoSimulacao;

    public async Task<Resultado> DefinirModoAsync(int id, ModoListaExterna modo, CancellationToken ct = default)
    {
        var lista = await listas.ObterPorIdAsync(id, ct);
        if (lista is null)
            return Resultado.Falha("Lista não encontrada.");
        if (!Enum.IsDefined(modo))
            return Resultado.Falha("Modo inválido.");

        lista.DefinirModo(modo);
        auditoria.Registrar("Modo da lista externa alterado", lista.Nome, modo.ToString());
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> SolicitarAtualizacaoAsync(int id, CancellationToken ct = default)
    {
        var lista = await listas.ObterPorIdAsync(id, ct);
        if (lista is null)
            return Resultado.Falha("Lista não encontrada.");
        if (!lista.EstaEmUso)
            return Resultado.Falha("Coloque a lista em avaliação ou ative-a antes de atualizar.");

        lista.SolicitarAtualizacao();
        auditoria.Registrar("Atualização de lista externa solicitada", lista.Nome);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
