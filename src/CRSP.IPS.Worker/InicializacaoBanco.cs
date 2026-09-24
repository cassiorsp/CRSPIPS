using CRSP.IPS.Infrastructure.Persistencia;

namespace CRSP.IPS.Worker;

/// <summary>Liberado quando as migrations terminam. Os trabalhadores aguardam este sinal antes de acessar o banco.</summary>
internal sealed class SinalBancoPronto
{
    private readonly TaskCompletionSource _pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task AguardarAsync(CancellationToken ct) => _pronto.Task.WaitAsync(ct);

    public void Concluir() => _pronto.TrySetResult();

    public void Falhar(Exception ex) => _pronto.TrySetException(ex);
}

/// <summary>
/// Aplica as migrations em segundo plano. Fica fora do Program.cs para o servico responder ao Gerenciador de
/// Servicos do Windows imediatamente; migrations demoradas antes do host iniciar causam o erro 1053.
/// </summary>
internal sealed class InicializacaoBanco(
    IServiceProvider servicos,
    SinalBancoPronto sinal,
    IHostApplicationLifetime ciclo,
    ILogger<InicializacaoBanco> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Libera o StartAsync do host antes de qualquer trabalho sincrono das migrations.
        await Task.Yield();
        try
        {
            await InicializadorBanco.InicializarAsync(servicos, stoppingToken);
            sinal.Concluir();
            logger.LogInformation("Banco de dados pronto");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            sinal.Falhar(new OperationCanceledException(stoppingToken));
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Falha ao inicializar o banco de dados. O servico sera encerrado");
            RegistroFalhaInicializacao.Gravar(ex);
            sinal.Falhar(ex);
            Environment.ExitCode = 1;
            ciclo.StopApplication();
        }
    }
}

/// <summary>Grava falhas fatais de inicializacao em arquivo, pois nesse ponto o log do Windows pode nao estar disponivel.</summary>
internal static class RegistroFalhaInicializacao
{
    public static void Gravar(Exception ex)
    {
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CRSPIPS", "logs");
            Directory.CreateDirectory(pasta);
            File.AppendAllText(Path.Combine(pasta, "worker-falhas.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Sem permissao de escrita: nao ha onde registrar.
        }
    }
}
