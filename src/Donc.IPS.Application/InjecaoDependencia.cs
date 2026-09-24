using Donc.IPS.Application.Motor;
using Donc.IPS.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Donc.IPS.Application;

public static class InjecaoDependencia
{
    /// <summary>Servicos de caso de uso usados pelo painel web e pelo Worker.</summary>
    public static IServiceCollection AdicionarAplicacao(this IServiceCollection services)
    {
        services.AddScoped<ServicoProtecao>();
        services.AddScoped<ServicoAuditoria>();
        services.AddScoped<ServicoBloqueios>();
        services.AddScoped<ServicoListas>();
        services.AddScoped<ServicoRegras>();
        services.AddScoped<ServicoConfiguracao>();
        services.AddScoped<ServicoUsuarios>();
        services.AddScoped<ServicoPainel>();
        services.AddScoped<ServicoGeoIp>();
        services.AddScoped<ServicoHistorico>();
        services.AddScoped<ServicoListasExternas>();
        return services;
    }

    /// <summary>Motor de deteccao e sincronizacao. Somente o Worker registra.</summary>
    public static IServiceCollection AdicionarMotor(this IServiceCollection services)
    {
        services.AddSingleton<ContadorJanelaDeslizante>();
        services.AddSingleton<LimitadorAmostras>();
        services.AddSingleton<AvaliadorRegras>();
        services.AddSingleton<EstadoMotor>();
        services.AddSingleton<MapaListasExternas>();
        services.AddScoped<ServicoAtualizacaoListasExternas>();
        services.AddScoped<ServicoDeteccao>();
        services.AddScoped<ServicoSincronizacaoFirewall>();
        services.AddScoped<ServicoPoliticaPaisesFirewall>();
        services.AddScoped<ServicoManutencao>();
        services.AddScoped<ServicoAtualizacaoGeo>();
        services.AddScoped<ServicoCompletarLocalizacao>();
        return services;
    }
}
