using System.Runtime.Versioning;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Infrastructure.Firewall;
using CRSP.IPS.Infrastructure.Fontes;
using CRSP.IPS.Infrastructure.Geolocalizacao;
using CRSP.IPS.Infrastructure.Persistencia;
using CRSP.IPS.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Infrastructure;

public static class InjecaoDependencia
{
    /// <summary>Persistencia, geolocalizacao e seguranca. Usado pelo painel web e pelo Worker.</summary>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<OpcoesCrspips>(configuracao.GetSection(OpcoesCrspips.Secao));
        var opcoes = configuracao.GetSection(OpcoesCrspips.Secao).Get<OpcoesCrspips>() ?? new OpcoesCrspips();

        var pastaBanco = Path.GetDirectoryName(Path.GetFullPath(opcoes.CaminhoBanco));
        if (!string.IsNullOrEmpty(pastaBanco))
            Directory.CreateDirectory(pastaBanco);

        services.AddSingleton<InterceptorSqlite>();
        services.AddDbContext<ContextoIps>((provedor, builder) => builder
            .UseSqlite($"Data Source={opcoes.CaminhoBanco};Default Timeout=30")
            .AddInterceptors(provedor.GetRequiredService<InterceptorSqlite>()));
        services.AddScoped<IUnidadeDeTrabalho>(provedor => provedor.GetRequiredService<ContextoIps>());

        services.AddScoped<IRepositorioBloqueios, RepositorioBloqueios>();
        services.AddScoped<IRepositorioRegras, RepositorioRegras>();
        services.AddScoped<IRepositorioListas, RepositorioListas>();
        services.AddScoped<IRepositorioConfiguracao, RepositorioConfiguracao>();
        services.AddScoped<IRepositorioConfiguracaoGeoIp, RepositorioConfiguracaoGeoIp>();
        services.AddScoped<IRepositorioEventos, RepositorioEventos>();
        services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
        services.AddScoped<IRepositorioAuditoria, RepositorioAuditoria>();
        services.AddScoped<IRepositorioPosicoesLeitura, RepositorioPosicoesLeitura>();
        services.AddScoped<IRepositorioStatusWorker, RepositorioStatusWorker>();
        services.AddScoped<IRepositorioRegrasFirewallDesativadas, RepositorioRegrasFirewallDesativadas>();
        services.AddScoped<IRepositorioListasExternas, RepositorioListasExternas>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IServicoGeolocalizacao, ServicoGeolocalizacaoMaxMind>();
        services.AddSingleton<IProvedorFaixasPais, ProvedorFaixasPaisMaxMind>();
        services.AddSingleton<IProvedorEnderecosLocais, ProvedorEnderecosLocais>();
        services.AddSingleton<IHashSenha, HashSenhaIdentity>();
        services.AddSingleton<IProtetorSegredos, ProtetorSegredosDpapi>();
        return services;
    }

    /// <summary>Firewall e fontes de eventos. Somente o Worker (com privilegio de administrador) registra.</summary>
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AdicionarInfraestruturaMotor(this IServiceCollection services)
    {
        services.AddSingleton<IServicoFirewall, FirewallWindows>();
        services.AddSingleton<EstadoLeitoresArquivo>();
        services.AddSingleton(provedor => new ResolvedorSitesIis(provedor.GetRequiredService<ILogger<ResolvedorSitesIis>>()));
        services.AddScoped<IFonteEventos, LeitorLogIis>();
        services.AddScoped<IFonteEventos, LeitorHttpErr>();
        services.AddScoped<IFonteEventos, LeitorEventosWindows>();

        services.AddHttpClient(AtualizadorBaseGeoMaxMind.NomeClienteHttp, cliente =>
            {
                cliente.Timeout = TimeSpan.FromMinutes(10);
                cliente.DefaultRequestHeaders.UserAgent.ParseAdd("CRSPIPS/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddScoped<IAtualizadorBaseGeo, AtualizadorBaseGeoMaxMind>();

        services.AddHttpClient(BaixadorListasExternas.NomeClienteHttp, cliente =>
        {
            cliente.Timeout = TimeSpan.FromMinutes(2);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("CRSPIPS/1.0");
        });
        services.AddScoped<IBaixadorListasExternas, BaixadorListasExternas>();
        return services;
    }
}
