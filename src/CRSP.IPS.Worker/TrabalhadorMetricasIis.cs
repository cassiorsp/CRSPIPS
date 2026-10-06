using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Motor;

namespace CRSP.IPS.Worker;

/// <summary>
/// A cada 30 segundos le memoria e CPU dos processos w3wp (por application pool) e grava a janela de 5 minutos
/// (minimo, maximo e media). A retencao das metricas roda junto com a dos eventos, no TrabalhadorFirewall.
/// </summary>
internal sealed class TrabalhadorMetricasIis(
    IServiceScopeFactory fabricaEscopo,
    IAmostradorProcessosIis amostrador,
    SinalBancoPronto bancoPronto,
    ILogger<TrabalhadorMetricasIis> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bancoPronto.AguardarAsync(stoppingToken);

        logger.LogInformation("Monitoramento do IIS iniciado");
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                var amostras = amostrador.Amostrar();
                var sites = amostrador.ListarSites();

                await using var escopo = fabricaEscopo.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ServicoMetricasIis>()
                    .RegistrarProcessosAsync(amostras, sites, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no monitoramento dos processos do IIS");
                await RegistrarErroAsync(ex, stoppingToken);
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RegistrarErroAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            await using var escopo = fabricaEscopo.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<ServicoManutencao>().RegistrarErroAsync($"Monitoramento do IIS: {ex.Message}", ct);
        }
        catch (Exception falha)
        {
            logger.LogDebug(falha, "Nao foi possivel registrar o erro no status do Worker");
        }
    }
}
