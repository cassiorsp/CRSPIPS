using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Domain.Entidades;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Motor;

/// <summary>Heartbeat do Worker e limpeza de dados antigos.</summary>
public sealed class ServicoManutencao(
    IRepositorioStatusWorker statusWorker,
    IRepositorioConfiguracao configuracoes,
    IRepositorioEventos eventos,
    IRepositorioListasExternas listasExternas,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoManutencao> logger)
{
    public async Task RegistrarInicioAsync(string maquina, string versao, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var status = await statusWorker.ObterAsync(ct);
        if (status is null)
            statusWorker.Adicionar(StatusWorker.Iniciar(maquina, versao, agora));
        else
            status.Reiniciar(maquina, versao, agora);
        await unidadeDeTrabalho.SalvarAsync(ct);
    }

    public async Task RegistrarSinalAsync(int eventosProcessados, CancellationToken ct = default)
    {
        var status = await statusWorker.ObterAsync(ct);
        if (status is null)
            return;
        status.RegistrarSinal(relogio.GetUtcNow().UtcDateTime);
        status.RegistrarEventosProcessados(eventosProcessados);
        await unidadeDeTrabalho.SalvarAsync(ct);
    }

    public async Task RegistrarErroAsync(string erro, CancellationToken ct = default)
    {
        var status = await statusWorker.ObterAsync(ct);
        if (status is null)
            return;
        status.RegistrarErro(erro, relogio.GetUtcNow().UtcDateTime);
        await unidadeDeTrabalho.SalvarAsync(ct);
    }

    public async Task AplicarRetencaoAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        var limite = relogio.GetUtcNow().UtcDateTime.AddDays(-configuracao.RetencaoEventosDias);
        var removidos = await eventos.RemoverAnterioresAsync(limite, ct);
        var coincidencias = await listasExternas.RemoverCoincidenciasAnterioresAsync(limite, ct);
        if (removidos > 0 || coincidencias > 0)
            logger.LogInformation(
                "Retencao: {Eventos} eventos e {Coincidencias} coincidencias de listas externas removidos",
                removidos, coincidencias);
    }
}
