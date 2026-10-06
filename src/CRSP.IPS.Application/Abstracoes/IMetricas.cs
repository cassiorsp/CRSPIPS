using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;

namespace CRSP.IPS.Application.Abstracoes;

public interface IRepositorioMetricas
{
    // Escrita (Worker): linhas rastreadas para somar sobre o que ja foi gravado na mesma hora/janela.
    Task<IReadOnlyList<MetricaEndpoint>> ObterEndpointsDasHorasAsync(IReadOnlyCollection<DateTime> horasUtc, CancellationToken ct = default);
    void AdicionarEndpoint(MetricaEndpoint metrica);
    Task<IReadOnlyList<MetricaProcessoIis>> ObterJanelaProcessosAsync(DateTime inicioUtc, CancellationToken ct = default);
    void AdicionarProcesso(MetricaProcessoIis metrica);
    Task<IReadOnlyList<SiteIis>> ListarSitesAsync(CancellationToken ct = default);
    void AdicionarSite(SiteIis site);
    Task<int> RemoverEndpointsAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default);
    Task<int> RemoverProcessosAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default);

    // Leitura (painel).
    Task<IReadOnlyList<LinhaRequisicoes>> ResumirEndpointsAsync(DateTime desdeUtc, string? site, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaRequisicoes>> ResumirSitesAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<IReadOnlyList<RequisicoesHora>> ContarRequisicoesPorHoraAsync(DateTime desdeUtc, IReadOnlyCollection<string>? sites, CancellationToken ct = default);
    Task<IReadOnlyList<ResumoPool>> ResumirPoolsAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<IReadOnlyList<MetricaProcessoIis>> ListarProcessosAsync(DateTime desdeUtc, string pool, CancellationToken ct = default);
}

/// <summary>Le os processos w3wp do IIS. Registrado somente no Worker.</summary>
public interface IAmostradorProcessosIis
{
    IReadOnlyList<AmostraProcessoIis> Amostrar();

    /// <summary>Sites configurados no IIS e o application pool de cada um.</summary>
    IReadOnlyList<SitePool> ListarSites();
}
