using CRSP.IPS.Application;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Infrastructure;
using CRSP.IPS.Worker;
using Microsoft.Extensions.Logging.EventLog;

try
{
    // Como servico do Windows o diretorio atual e C:\Windows\System32: o appsettings.json deve vir da pasta do executavel.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Services.AddWindowsService(opcoes => opcoes.ServiceName = "CRSPIPS");

    // Origem fixa no Visualizador de Eventos (Aplicativo > CRSPIPS). Sem isso o .NET usa ".NET Runtime" ou o nome do executavel.
    builder.Services.Configure<EventLogSettings>(opcoes =>
    {
        opcoes.SourceName = "CRSPIPS";
        opcoes.LogName = "Application";
    });

    builder.Services
        .AdicionarInfraestrutura(builder.Configuration)
        .AdicionarInfraestruturaMotor()
        .AdicionarAplicacao()
        .AdicionarMotor();

    builder.Services.AddSingleton<IContextoUsuario, ContextoUsuarioSistema>();
    builder.Services.AddSingleton<SinalBancoPronto>();
    builder.Services.AddHostedService<InicializacaoBanco>();
    builder.Services.AddHostedService<TrabalhadorDeteccao>();
    builder.Services.AddHostedService<TrabalhadorMetricasIis>();
    builder.Services.AddHostedService<TrabalhadorFirewall>();
    builder.Services.AddHostedService<TrabalhadorGeoIp>();
    builder.Services.AddHostedService<TrabalhadorListasExternas>();

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    RegistroFalhaInicializacao.Gravar(ex);
    throw;
}
