using System.Reflection;
using Donc.IPS.Application.Motor;

namespace Donc.IPS.Worker;

/// <summary>
/// A cada 2 segundos reconcilia o Windows Firewall com o estado desejado no banco (bloqueios, lista negra
/// e politica de paises). E isso que faz as acoes manuais do painel refletirem quase instantaneamente.
/// </summary>
internal sealed class TrabalhadorFirewall(IServiceScopeFactory fabricaEscopo, SinalBancoPronto bancoPronto, ILogger<TrabalhadorFirewall> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IntervaloRetencao = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bancoPronto.AguardarAsync(stoppingToken);

        await using (var escopo = fabricaEscopo.CreateAsyncScope())
        {
            var versao = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            await escopo.ServiceProvider.GetRequiredService<ServicoManutencao>()
                .RegistrarInicioAsync(Environment.MachineName, versao, stoppingToken);
        }

        logger.LogInformation("Sincronizacao do firewall iniciada");
        var ultimaRetencao = DateTime.MinValue;
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await using var escopo = fabricaEscopo.CreateAsyncScope();
                var provedor = escopo.ServiceProvider;

                await provedor.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync(stoppingToken);
                await provedor.GetRequiredService<ServicoPoliticaPaisesFirewall>().AplicarAsync(stoppingToken);

                if (DateTime.UtcNow - ultimaRetencao >= IntervaloRetencao)
                {
                    await provedor.GetRequiredService<ServicoManutencao>().AplicarRetencaoAsync(stoppingToken);
                    ultimaRetencao = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na sincronizacao do firewall");
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
            await escopo.ServiceProvider.GetRequiredService<ServicoManutencao>().RegistrarErroAsync($"Firewall: {ex.Message}", ct);
        }
        catch (Exception falha)
        {
            logger.LogDebug(falha, "Nao foi possivel registrar o erro no status do Worker");
        }
    }
}
