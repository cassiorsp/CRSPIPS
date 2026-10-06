using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;

namespace CRSP.IPS.Application.Servicos;

/// <summary>Consultas de leitura das telas Endpoints e IIS.</summary>
public sealed class ServicoMetricasPainel(
    IRepositorioMetricas metricas,
    IRepositorioStatusWorker statusWorker,
    TimeProvider relogio)
{
    public async Task<PainelEndpoints> ObterEndpointsAsync(PeriodoDashboard periodo, string? site, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var fuso = relogio.LocalTimeZone;
        var desde = ServicoPainel.CalcularInicioUtc(periodo, agora, fuso);
        var granularidade = periodo == PeriodoDashboard.Hoje ? GranularidadeSerie.Hora : GranularidadeSerie.Dia;

        var sitesDoPeriodo = (await metricas.ResumirSitesAsync(desde, ct))
            .Select(s => s.Site)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (site is not null && !sitesDoPeriodo.Contains(site))
            site = null;

        var endpoints = (await metricas.ResumirEndpointsAsync(desde, site, ct))
            .OrderByDescending(l => l.Total)
            .ToList();
        var horas = await metricas.ContarRequisicoesPorHoraAsync(desde, site is null ? null : [site], ct);

        return new PainelEndpoints(
            periodo,
            granularidade,
            site,
            sitesDoPeriodo,
            Totalizar(endpoints),
            MontarSerieRequisicoes(horas, desde, agora, fuso, granularidade),
            endpoints,
            await WorkerOnlineAsync(agora, ct),
            endpoints.Count > 0);
    }

    public async Task<PainelIis> ObterIisAsync(PeriodoDashboard periodo, string? pool, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var fuso = relogio.LocalTimeZone;
        var desde = ServicoPainel.CalcularInicioUtc(periodo, agora, fuso);
        var emJanelas = periodo == PeriodoDashboard.Hoje;
        var granularidade = periodo <= PeriodoDashboard.Dias7 ? GranularidadeSerie.Hora : GranularidadeSerie.Dia;

        var pools = (await metricas.ResumirPoolsAsync(desde, ct)).OrderByDescending(p => p.MemoriaMaxima).ToList();
        var sitesIis = await metricas.ListarSitesAsync(ct);
        var poolPorSite = sitesIis.ToDictionary(s => s.Nome, s => s.Pool, StringComparer.OrdinalIgnoreCase);
        var consumoPorPool = pools.ToDictionary(p => p.Pool, StringComparer.OrdinalIgnoreCase);

        var linhasSites = await metricas.ResumirSitesAsync(desde, ct);
        var resumoSites = linhasSites
            .Select(l =>
            {
                var poolDoSite = poolPorSite.GetValueOrDefault(l.Site);
                return new ResumoSiteIis(l.Site, poolDoSite, Totalizar([l]),
                    poolDoSite is not null ? consumoPorPool.GetValueOrDefault(poolDoSite) : null);
            })
            // Sites do IIS sem requisicoes no periodo tambem aparecem, zerados.
            .Concat(sitesIis
                .Where(s => linhasSites.All(l => !l.Site.Equals(s.Nome, StringComparison.OrdinalIgnoreCase)))
                .Select(s => new ResumoSiteIis(s.Nome, s.Pool, Totalizar([]), consumoPorPool.GetValueOrDefault(s.Pool))))
            .OrderByDescending(s => s.Requisicoes.Total)
            .ThenBy(s => s.Site, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (pool is null || !consumoPorPool.ContainsKey(pool))
            pool = pools.FirstOrDefault()?.Pool;

        var serieMemoria = new List<PontoMemoria>();
        var serieRequisicoes = new List<PontoRequisicoes>();
        if (pool is not null)
        {
            var janelas = await metricas.ListarProcessosAsync(desde, pool, ct);
            serieMemoria = MontarSerieMemoria(janelas, fuso, emJanelas, granularidade);

            var sitesDoPool = poolPorSite.Where(p => p.Value.Equals(pool, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToList();
            if (sitesDoPool.Count > 0)
            {
                var horas = await metricas.ContarRequisicoesPorHoraAsync(desde, sitesDoPool, ct);
                serieRequisicoes = MontarSerieRequisicoes(horas, desde, agora, fuso, granularidade);
            }
        }

        return new PainelIis(periodo, granularidade, emJanelas, resumoSites, pools, pool, serieMemoria, serieRequisicoes, await WorkerOnlineAsync(agora, ct));
    }

    private async Task<bool> WorkerOnlineAsync(DateTime agora, CancellationToken ct) =>
        (await statusWorker.ObterAsync(ct))?.EstaOnline(agora) ?? false;

    private static TotaisRequisicoes Totalizar(IReadOnlyList<LinhaRequisicoes> linhas)
    {
        var total = linhas.Sum(l => l.Total);
        var tempoTotal = linhas.Sum(l => l.TempoTotalMs);
        return new TotaisRequisicoes(
            total,
            linhas.Sum(l => l.Sucesso),
            linhas.Sum(l => l.Redirecionamento),
            linhas.Sum(l => l.ErroCliente),
            linhas.Sum(l => l.ErroServidor),
            total == 0 ? 0 : (double)tempoTotal / total,
            linhas.Count == 0 ? 0 : linhas.Max(l => l.TempoMaximoMs));
    }

    private static List<PontoRequisicoes> MontarSerieRequisicoes(
        IReadOnlyList<RequisicoesHora> horas, DateTime desdeUtc, DateTime agoraUtc, TimeZoneInfo fuso, GranularidadeSerie granularidade)
    {
        DateTime Truncar(DateTime local) => granularidade == GranularidadeSerie.Hora
            ? new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0)
            : local.Date;
        DateTime Avancar(DateTime local) => granularidade == GranularidadeSerie.Hora ? local.AddHours(1) : local.AddDays(1);

        var grupos = horas
            .GroupBy(h => Truncar(TimeZoneInfo.ConvertTimeFromUtc(h.HoraUtc, fuso)))
            .ToDictionary(g => g.Key, g => new PontoRequisicoes(g.Key, g.Sum(h => h.Sucesso), g.Sum(h => h.ErroCliente), g.Sum(h => h.ErroServidor)));

        var serie = new List<PontoRequisicoes>();
        var fim = Truncar(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, fuso));
        for (var atual = Truncar(TimeZoneInfo.ConvertTimeFromUtc(desdeUtc, fuso)); atual <= fim; atual = Avancar(atual))
            serie.Add(grupos.GetValueOrDefault(atual) ?? new PontoRequisicoes(atual, 0, 0, 0));
        return serie;
    }

    private static List<PontoMemoria> MontarSerieMemoria(
        IReadOnlyList<MetricaProcessoIis> janelas, TimeZoneInfo fuso, bool emJanelas, GranularidadeSerie granularidade)
    {
        DateTime Truncar(DateTime local) => emJanelas ? local
            : granularidade == GranularidadeSerie.Hora
                ? new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0)
                : local.Date;

        return janelas
            .GroupBy(j => Truncar(TimeZoneInfo.ConvertTimeFromUtc(j.InicioUtc, fuso)))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var amostras = Math.Max(1, g.Sum(j => j.Amostras));
                return new PontoMemoria(
                    g.Key,
                    g.Min(j => j.MemoriaMinima),
                    g.Max(j => j.MemoriaMaxima),
                    g.Sum(j => j.MemoriaSoma) / amostras,
                    g.Sum(j => j.CpuSoma) / amostras);
            })
            .ToList();
    }
}
