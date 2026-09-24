using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Modelos;

namespace CRSP.IPS.Worker;

/// <summary>A cada 5 segundos le as fontes habilitadas e envia os eventos para o motor de deteccao.</summary>
internal sealed class TrabalhadorDeteccao(IServiceScopeFactory fabricaEscopo, SinalBancoPronto bancoPronto, ILogger<TrabalhadorDeteccao> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await bancoPronto.AguardarAsync(stoppingToken);

        logger.LogInformation("Deteccao iniciada");
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no ciclo de deteccao");
                await RegistrarErroAsync(ex, stoppingToken);
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ExecutarCicloAsync(CancellationToken ct)
    {
        await using var escopo = fabricaEscopo.CreateAsyncScope();
        var provedor = escopo.ServiceProvider;
        var configuracao = await provedor.GetRequiredService<IRepositorioConfiguracao>().ObterAsync(ct);

        var eventos = new List<EventoDetectado>();
        foreach (var fonte in provedor.GetServices<IFonteEventos>())
        {
            if (!fonte.EstaHabilitada(configuracao))
                continue;
            var lidos = await fonte.LerNovosEventosAsync(configuracao, ct);
            if (lidos.Count > 0)
                logger.LogDebug("Fonte {Fonte}: {Quantidade} eventos", fonte.Fonte, lidos.Count);
            eventos.AddRange(lidos);
        }

        var bloqueados = await provedor.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos, ct);
        if (bloqueados > 0)
            logger.LogInformation("Ciclo de deteccao: {Eventos} eventos, {Bloqueados} novos bloqueios", eventos.Count, bloqueados);

        await provedor.GetRequiredService<ServicoManutencao>().RegistrarSinalAsync(eventos.Count, ct);
    }

    private async Task RegistrarErroAsync(Exception ex, CancellationToken ct)
    {
        try
        {
            await using var escopo = fabricaEscopo.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<ServicoManutencao>().RegistrarErroAsync($"Detecção: {ex.Message}", ct);
        }
        catch (Exception falha)
        {
            logger.LogDebug(falha, "Nao foi possivel registrar o erro no status do Worker");
        }
    }
}
