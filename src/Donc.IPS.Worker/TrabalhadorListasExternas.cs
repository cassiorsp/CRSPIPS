using Donc.IPS.Application.Motor;

namespace Donc.IPS.Worker;

/// <summary>
/// A cada minuto verifica se alguma lista externa venceu o intervalo ou teve atualizacao pedida no painel.
/// Separado da sincronizacao do firewall para que um download lento nao atrase bloqueios.
/// </summary>
internal sealed class TrabalhadorListasExternas(
    IServiceScopeFactory fabricaEscopo,
    SinalBancoPronto bancoPronto,
    ILogger<TrabalhadorListasExternas> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bancoPronto.AguardarAsync(stoppingToken);

        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await using var escopo = fabricaEscopo.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ServicoAtualizacaoListasExternas>().AtualizarPendentesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na atualizacao das listas externas");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
