using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Servicos;

/// <summary>
/// Limpeza do historico (eventos e bloqueios) para recomecar a calibracao. Usuarios, regras, listas,
/// configuracoes e auditoria sao preservados; a propria limpeza fica registrada na auditoria.
/// </summary>
public sealed class ServicoHistorico(
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IRepositorioListasExternas listasExternas,
    ServicoAuditoria auditoria,
    IContextoUsuario contexto,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    ILogger<ServicoHistorico> logger)
{
    public async Task<Resultado<(int Bloqueios, int Eventos)>> LimparAsync(bool manterBloqueiosAtivos, CancellationToken ct = default)
    {
        var bloqueiosRemovidos = await bloqueios.RemoverHistoricoAsync(manterBloqueiosAtivos, ct);
        var eventosRemovidos = await eventos.RemoverTodosAsync(ct);
        await listasExternas.RemoverCoincidenciasAnterioresAsync(DateTime.MaxValue, ct);

        auditoria.Registrar("Histórico limpo", "Bloqueios e eventos",
            $"{bloqueiosRemovidos} bloqueios, {eventosRemovidos} eventos. Bloqueios ativos mantidos: {manterBloqueiosAtivos}");
        await unidadeDeTrabalho.SalvarAsync(ct);

        logger.LogWarning(
            "Historico limpo por {Usuario}: {Bloqueios} bloqueios e {Eventos} eventos removidos, bloqueios ativos mantidos {Manter}",
            contexto.Nome, bloqueiosRemovidos, eventosRemovidos, manterBloqueiosAtivos);
        return Resultado<(int, int)>.Ok((bloqueiosRemovidos, eventosRemovidos));
    }
}
