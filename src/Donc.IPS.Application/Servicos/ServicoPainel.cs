using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Enums;

namespace Donc.IPS.Application.Servicos;

/// <summary>Consultas de leitura para Dashboard e Monitor.</summary>
public sealed class ServicoPainel(
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IRepositorioRegras regras,
    IRepositorioConfiguracao configuracoes,
    IRepositorioStatusWorker statusWorker,
    TimeProvider relogio)
{
    public async Task<IndicadoresDashboard> ObterIndicadoresAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var fuso = relogio.LocalTimeZone;
        var ultimas24h = agora.AddHours(-24);
        var inicioHojeUtc = TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTimeFromUtc(agora, fuso).Date, fuso);

        var configuracao = await configuracoes.ObterAsync(ct);
        var status = await statusWorker.ObterAsync(ct);
        var ativos = await bloqueios.ListarAtivosAsync(ct);
        var ultimos30Dias = await bloqueios.ListarDesdeAsync(agora.AddDays(-30), ct);
        var momentosEventos = await eventos.ListarMomentosDesdeAsync(ultimas24h, ct);

        var ultimos7Dias = ultimos30Dias.Where(b => b.BloqueadoEm >= agora.AddDays(-7)).ToList();

        var topPaises = ultimos7Dias
            .Where(b => b.PaisCodigo is not null)
            .GroupBy(b => b.PaisCodigo!)
            .Select(g => new ItemRanking(g.Key, g.Count(), g.First().PaisNome))
            .OrderByDescending(i => i.Quantidade)
            .Take(10)
            .ToList();

        // Ordenado pelos eventos registrados (volume real de tentativas), nao pelo numero no momento do bloqueio,
        // que e praticamente o gatilho da regra. Complemento = "bloqueios|pais".
        var bloqueiosPorIp = ultimos30Dias.GroupBy(b => b.Ip).ToDictionary(g => g.Key, g => g.Count());
        var topIps = (await eventos.RankingIpsDesdeAsync(agora.AddDays(-30), 10, ct))
            .Select(i => i with { Complemento = $"{bloqueiosPorIp.GetValueOrDefault(i.Rotulo)}|{i.Complemento}" })
            .ToList();

        return new IndicadoresDashboard(
            BloqueiosAtivos: ativos.Count(b => !b.Simulado),
            BloqueiosSimuladosAtivos: ativos.Count(b => b.Simulado),
            BloqueadosHoje: ultimos30Dias.Count(b => b.BloqueadoEm >= inicioHojeUtc && !b.Simulado),
            BloqueadosHojeSimulados: ultimos30Dias.Count(b => b.BloqueadoEm >= inicioHojeUtc && b.Simulado),
            EventosUltimas24h: momentosEventos.Count,
            PaisesUltimas24h: await eventos.ContarPaisesDistintosDesdeAsync(ultimas24h, ct),
            ModoSimulacao: configuracao.ModoSimulacao,
            ModoPaises: configuracao.ModoPaises,
            Worker: status,
            WorkerOnline: status?.EstaOnline(agora) ?? false,
            SerieBloqueios: MontarSerieHoraria(ultimos30Dias.Where(b => b.BloqueadoEm >= ultimas24h).Select(b => b.BloqueadoEm), agora, fuso),
            SerieEventos: MontarSerieHoraria(momentosEventos, agora, fuso),
            TopPaises: topPaises,
            TopIps: topIps,
            TopUrls: await eventos.RankingUrlsDesdeAsync(agora.AddDays(-7), 10, ct),
            EventosPorFonte: await eventos.ContarPorFonteDesdeAsync(ultimas24h, ct),
            BloqueiosRecentes: ultimos30Dias.OrderByDescending(b => b.BloqueadoEm).Take(10).ToList());
    }

    public async Task<IReadOnlyList<EventoResumo>> ListarEventosRecentesAsync(TipoFonte? fonte, long? aposId, CancellationToken ct = default)
    {
        var nomesRegras = (await regras.ListarAsync(ct)).ToDictionary(r => r.Id, r => r.Nome);
        return (await eventos.ListarRecentesAsync(200, fonte, aposId, ct))
            .Select(e => ServicoBloqueios.MapearEvento(e, nomesRegras))
            .ToList();
    }

    private static List<PontoSerie> MontarSerieHoraria(IEnumerable<DateTime> momentosUtc, DateTime agoraUtc, TimeZoneInfo fuso)
    {
        var horaAtual = new DateTime(agoraUtc.Year, agoraUtc.Month, agoraUtc.Day, agoraUtc.Hour, 0, 0, DateTimeKind.Utc);
        var contagem = momentosUtc
            .GroupBy(m => new DateTime(m.Year, m.Month, m.Day, m.Hour, 0, 0, DateTimeKind.Utc))
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, 24)
            .Select(i => horaAtual.AddHours(i - 23))
            .Select(hora => new PontoSerie(
                TimeZoneInfo.ConvertTimeFromUtc(hora, fuso).ToString("HH:00"),
                contagem.GetValueOrDefault(hora)))
            .ToList();
    }
}
