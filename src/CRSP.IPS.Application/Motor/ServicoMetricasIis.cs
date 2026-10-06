using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Monitoramento do IIS: agrega TODAS as requisicoes do log (total, sucesso e erro por endpoint) e o consumo dos
/// application pools. Somente agregados sao gravados, nunca uma linha por requisicao.
/// </summary>
public sealed class ServicoMetricasIis(
    IRepositorioMetricas metricas,
    IRepositorioConfiguracao configuracoes,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoMetricasIis> logger)
{
    public static readonly TimeSpan JanelaProcessos = TimeSpan.FromMinutes(5);

    /// <summary>Limite de endpoints distintos por site e hora; o excedente cai em "(outros)" (protege o banco de URLs aleatorias).</summary>
    private const int MaximoEndpointsPorSiteHora = 500;

    private const string SiteDesconhecido = "(desconhecido)";

    private static readonly long TicksHora = TimeSpan.FromHours(1).Ticks;

    public async Task<int> RegistrarRequisicoesAsync(IReadOnlyList<EventoDetectado> eventos, CancellationToken ct = default)
    {
        var requisicoes = eventos.Where(e => e.Fonte == TipoFonte.LogIis).ToList();
        if (requisicoes.Count == 0)
            return 0;

        var horas = requisicoes.Select(e => TruncarHora(e.OcorridoEmUtc)).Distinct().ToList();
        var existentes = (await metricas.ObterEndpointsDasHorasAsync(horas, ct))
            .ToDictionary(m => (m.HoraUtc, m.Site, m.Metodo, m.Endpoint));
        var endpointsPorSiteHora = existentes.Keys
            .GroupBy(k => (k.HoraUtc, k.Site))
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var evento in requisicoes)
        {
            var hora = TruncarHora(evento.OcorridoEmUtc);
            var site = string.IsNullOrEmpty(evento.Site) ? SiteDesconhecido : evento.Site;
            var metodo = string.IsNullOrEmpty(evento.Metodo) ? "-" : evento.Metodo.ToUpperInvariant();
            var endpoint = NormalizadorEndpoint.Normalizar(evento.Url);

            if (!existentes.TryGetValue((hora, site, metodo, endpoint), out var metrica))
            {
                var distintos = endpointsPorSiteHora.GetValueOrDefault((hora, site));
                if (distintos >= MaximoEndpointsPorSiteHora && endpoint != NormalizadorEndpoint.Estaticos)
                    endpoint = NormalizadorEndpoint.Outros;

                if (!existentes.TryGetValue((hora, site, metodo, endpoint), out metrica))
                {
                    metrica = MetricaEndpoint.Criar(hora, site, metodo, endpoint);
                    existentes[(hora, site, metodo, endpoint)] = metrica;
                    endpointsPorSiteHora[(hora, site)] = distintos + 1;
                    metricas.AdicionarEndpoint(metrica);
                }
            }

            metrica.Registrar(evento.CodigoStatus, evento.TempoMs);
        }

        await unidadeDeTrabalho.SalvarAsync(ct);
        return requisicoes.Count;
    }

    public async Task RegistrarProcessosAsync(IReadOnlyList<AmostraProcessoIis> amostras, IReadOnlyList<SitePool> sites, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        await AtualizarSitesAsync(sites, agora, ct);

        if (amostras.Count > 0)
        {
            var inicio = new DateTime(agora.Ticks - agora.Ticks % JanelaProcessos.Ticks, DateTimeKind.Utc);
            var janelas = (await metricas.ObterJanelaProcessosAsync(inicio, ct)).ToDictionary(m => m.Pool, StringComparer.OrdinalIgnoreCase);

            foreach (var pool in amostras.GroupBy(a => a.Pool, StringComparer.OrdinalIgnoreCase))
            {
                if (!janelas.TryGetValue(pool.Key, out var janela))
                {
                    janela = MetricaProcessoIis.Criar(inicio, pool.Key);
                    metricas.AdicionarProcesso(janela);
                }

                janela.Registrar(pool.Sum(a => a.MemoriaPrivadaBytes), pool.Sum(a => a.CpuPercentual), pool.Count());
            }
        }

        await unidadeDeTrabalho.SalvarAsync(ct);
    }

    public async Task AplicarRetencaoAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var configuracao = await configuracoes.ObterAsync(ct);
        var endpoints = await metricas.RemoverEndpointsAnterioresAsync(agora.AddDays(-configuracao.RetencaoMetricasEndpointsDias), ct);
        var processos = await metricas.RemoverProcessosAnterioresAsync(agora.AddDays(-configuracao.RetencaoMetricasProcessosDias), ct);
        if (endpoints > 0 || processos > 0)
            logger.LogInformation("Retencao de metricas: {Endpoints} registros de endpoints e {Processos} de processos removidos", endpoints, processos);
    }

    private async Task AtualizarSitesAsync(IReadOnlyList<SitePool> sites, DateTime agora, CancellationToken ct)
    {
        if (sites.Count == 0)
            return;

        var atuais = (await metricas.ListarSitesAsync(ct)).ToDictionary(s => s.Nome, StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites)
        {
            if (!atuais.TryGetValue(site.Nome, out var registro))
                metricas.AdicionarSite(SiteIis.Criar(site.Nome, site.Pool, agora));
            else if (!registro.Pool.Equals(site.Pool, StringComparison.Ordinal))
                registro.Atualizar(site.Pool, agora);
        }
    }

    private static DateTime TruncarHora(DateTime utc) => new(utc.Ticks - utc.Ticks % TicksHora, DateTimeKind.Utc);
}
