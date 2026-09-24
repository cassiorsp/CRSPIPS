using Donc.IPS.Application;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Infrastructure;
using Donc.IPS.Worker;

try
{
    // Como servico do Windows o diretorio atual e C:\Windows\System32: o appsettings.json deve vir da pasta do executavel.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });

    builder.Services.AddWindowsService(opcoes => opcoes.ServiceName = "CRSPIPS");

    builder.Services
        .AdicionarInfraestrutura(builder.Configuration)
        .AdicionarInfraestruturaMotor()
        .AdicionarAplicacao()
        .AdicionarMotor();

    builder.Services.AddSingleton<IContextoUsuario, ContextoUsuarioSistema>();
    builder.Services.AddSingleton<SinalBancoPronto>();
    builder.Services.AddHostedService<InicializacaoBanco>();
    builder.Services.AddHostedService<TrabalhadorDeteccao>();
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
