using CRSP.IPS.Application.Motor;

namespace CRSP.IPS.Worker;

/// <summary>
/// A cada 15 segundos verifica se ha pedido de atualizacao das bases GeoIP (botao "Atualizar agora")
/// ou se venceu a verificacao automatica de 24 horas. Fica separado da sincronizacao do firewall
/// para que um download demorado nao atrase bloqueios.
/// </summary>
internal sealed class TrabalhadorGeoIp(IServiceScopeFactory fabricaEscopo, SinalBancoPronto bancoPronto, ILogger<TrabalhadorGeoIp> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IntervaloComplementacao = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bancoPronto.AguardarAsync(stoppingToken);

        var ultimaComplementacao = DateTime.MinValue;
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await using var escopo = fabricaEscopo.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ServicoAtualizacaoGeo>().ExecutarSeNecessarioAsync(stoppingToken);

                if (DateTime.UtcNow - ultimaComplementacao >= IntervaloComplementacao)
                {
                    await escopo.ServiceProvider.GetRequiredService<ServicoCompletarLocalizacao>().ExecutarAsync(stoppingToken);
                    ultimaComplementacao = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na verificacao das bases GeoIP");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
