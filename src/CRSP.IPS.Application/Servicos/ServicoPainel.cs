using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;

namespace CRSP.IPS.Application.Servicos;

/// <summary>Consultas de leitura para Dashboard e Monitor.</summary>
public sealed class ServicoPainel(
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IRepositorioRegras regras,
    IRepositorioConfiguracao configuracoes,
    IRepositorioStatusWorker statusWorker,
    TimeProvider relogio)
{
    private const int TamanhoRankings = 50;

    public async Task<IndicadoresDashboard> ObterIndicadoresAsync(PeriodoDashboard periodo = PeriodoDashboard.Hoje, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var fuso = relogio.LocalTimeZone;
        var desde = CalcularInicioUtc(periodo, agora, fuso);

        var configuracao = await configuracoes.ObterAsync(ct);
        var status = await statusWorker.ObterAsync(ct);
        var resumo = await bloqueios.ResumirDesdeAsync(desde, ct);
        var horasEventos = await eventos.ContarPorHoraDesdeAsync(desde, ct);
        var horasBloqueios = await bloqueios.ContarPorHoraDesdeAsync(desde, ct);

        // Ordenado pelos eventos registrados (volume real de tentativas), nao pelo numero no momento do bloqueio,
        // que e praticamente o gatilho da regra.
        var rankingIps = await eventos.RankingIpsDesdeAsync(desde, TamanhoRankings, ct);
        var bloqueiosPorIp = await bloqueios.ContarPorIpsDesdeAsync(rankingIps.Select(i => i.Rotulo).ToList(), desde, ct);
        var topIps = rankingIps
            .Select(i => new IpAgressor(i.Rotulo, i.Complemento, i.Quantidade, bloqueiosPorIp.GetValueOrDefault(i.Rotulo)))
            .ToList();

        var todasHoras = horasEventos.Concat(horasBloqueios).Select(h => h.HoraUtc).ToList();
        var granularidade = DefinirGranularidade(periodo, todasHoras, agora);
        var inicioSerie = periodo == PeriodoDashboard.Tudo ? todasHoras.DefaultIfEmpty(agora).Min() : desde;

        return new IndicadoresDashboard(
            Periodo: periodo,
            Granularidade: granularidade,
            RetencaoEventosDias: configuracao.RetencaoEventosDias,
            BloqueiosAtivos: resumo.AtivosReais,
            BloqueiosSimuladosAtivos: resumo.AtivosSimulados,
            BloqueiosNoPeriodo: resumo.Reais,
            BloqueiosSimuladosNoPeriodo: resumo.Simulados,
            EventosNoPeriodo: horasEventos.Sum(h => h.Quantidade),
            PaisesNoPeriodo: await eventos.ContarPaisesDistintosDesdeAsync(desde, ct),
            ModoSimulacao: configuracao.ModoSimulacao,
            ModoPaises: configuracao.ModoPaises,
            Worker: status,
            WorkerOnline: status?.EstaOnline(agora) ?? false,
            SerieBloqueios: MontarSerie(horasBloqueios, inicioSerie, agora, fuso, granularidade),
            SerieEventos: MontarSerie(horasEventos, inicioSerie, agora, fuso, granularidade),
            TopPaises: await bloqueios.RankingPaisesDesdeAsync(desde, 10, ct),
            TopIps: topIps,
            TopUrls: await eventos.RankingUrlsDesdeAsync(desde, TamanhoRankings, ct),
            EventosPorFonte: await eventos.ContarPorFonteDesdeAsync(desde, ct),
            BloqueiosRecentes: await bloqueios.ListarRecentesDesdeAsync(desde, TamanhoRankings, ct));
    }

    public async Task<IReadOnlyList<EventoResumo>> ListarEventosRecentesAsync(FiltroEventos filtro, long? aposId, CancellationToken ct = default)
    {
        var nomesRegras = (await regras.ListarAsync(ct)).ToDictionary(r => r.Id, r => r.Nome);
        return (await eventos.ListarRecentesAsync(200, filtro, aposId, ct))
            .Select(e => ServicoBloqueios.MapearEvento(e, nomesRegras))
            .ToList();
    }

    public Task<OpcoesFiltroEventos> ObterOpcoesFiltroEventosAsync(CancellationToken ct = default) => eventos.ObterOpcoesFiltroAsync(ct);

    /// <summary>"Hoje" comeca a meia-noite do horario local do servidor; os demais contam dias corridos a partir de agora.</summary>
    public static DateTime CalcularInicioUtc(PeriodoDashboard periodo, DateTime agoraUtc, TimeZoneInfo fuso) => periodo switch
    {
        PeriodoDashboard.Tudo => DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
        PeriodoDashboard.Hoje => TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, fuso).Date, fuso),
        _ => agoraUtc.AddDays(-(int)periodo)
    };

    private static GranularidadeSerie DefinirGranularidade(PeriodoDashboard periodo, IReadOnlyList<DateTime> horasUtc, DateTime agoraUtc) => periodo switch
    {
        PeriodoDashboard.Hoje => GranularidadeSerie.Hora,
        PeriodoDashboard.Tudo => agoraUtc - horasUtc.DefaultIfEmpty(agoraUtc).Min() > TimeSpan.FromDays(120)
            ? GranularidadeSerie.Semana
            : GranularidadeSerie.Dia,
        >= PeriodoDashboard.Dias180 => GranularidadeSerie.Semana,
        _ => GranularidadeSerie.Dia
    };

    /// <summary>Distribui as contagens por hora UTC em intervalos locais (hora, dia ou semana), preenchendo os vazios com zero.</summary>
    private static List<PontoSerie> MontarSerie(
        IReadOnlyList<ContagemHora> horas, DateTime inicioUtc, DateTime agoraUtc, TimeZoneInfo fuso, GranularidadeSerie granularidade)
    {
        DateTime Truncar(DateTime local) => granularidade switch
        {
            GranularidadeSerie.Hora => new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0),
            GranularidadeSerie.Dia => local.Date,
            _ => local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7))
        };

        DateTime Avancar(DateTime local) => granularidade switch
        {
            GranularidadeSerie.Hora => local.AddHours(1),
            GranularidadeSerie.Dia => local.AddDays(1),
            _ => local.AddDays(7)
        };

        var contagem = horas
            .GroupBy(h => Truncar(TimeZoneInfo.ConvertTimeFromUtc(h.HoraUtc, fuso)))
            .ToDictionary(g => g.Key, g => g.Sum(h => h.Quantidade));

        var serie = new List<PontoSerie>();
        var fim = Truncar(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, fuso));
        for (var atual = Truncar(TimeZoneInfo.ConvertTimeFromUtc(inicioUtc, fuso)); atual <= fim; atual = Avancar(atual))
            serie.Add(new PontoSerie(atual, contagem.GetValueOrDefault(atual)));
        return serie;
    }
}
