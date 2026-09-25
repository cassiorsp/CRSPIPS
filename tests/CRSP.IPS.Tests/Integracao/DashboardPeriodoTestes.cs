using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class DashboardPeriodoTestes
{
    [Fact]
    public async Task TodosOsIndicadoresRespeitamOPeriodo()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.Geolocalizacao.Paises["203.0.113.7"] = "CN";
        ambiente.Geolocalizacao.Paises["198.51.100.23"] = "RU";

        // Ha 10 dias: um ataque de 203.0.113.7. Hoje: um de 198.51.100.23.
        await GerarAtaqueAsync(ambiente, "203.0.113.7", "/antigo");
        ambiente.Relogio.Advance(TimeSpan.FromDays(10));
        await GerarAtaqueAsync(ambiente, "198.51.100.23", "/recente");

        var hoje = await ObterAsync(ambiente, PeriodoDashboard.Hoje);
        var semana = await ObterAsync(ambiente, PeriodoDashboard.Dias7);
        var mes = await ObterAsync(ambiente, PeriodoDashboard.Dias30);
        var tudo = await ObterAsync(ambiente, PeriodoDashboard.Tudo);

        Assert.Equal(1, hoje.BloqueiosSimuladosNoPeriodo);
        Assert.Equal(1, semana.BloqueiosSimuladosNoPeriodo);
        Assert.Equal(2, mes.BloqueiosSimuladosNoPeriodo);
        Assert.Equal(2, tudo.BloqueiosSimuladosNoPeriodo);

        Assert.Equal(["198.51.100.23"], semana.TopIps.Select(i => i.Ip));
        Assert.Equal(["198.51.100.23", "203.0.113.7"], mes.TopIps.Select(i => i.Ip).Order());
        Assert.Equal(["/recente"], semana.TopUrls.Select(u => u.Rotulo));
        Assert.Equal(["RU"], semana.TopPaises.Select(p => p.Rotulo));
        Assert.Equal(1, semana.PaisesNoPeriodo);
        Assert.Equal(2, mes.PaisesNoPeriodo);
        Assert.Single(semana.BloqueiosRecentes);
        Assert.Equal(2, mes.BloqueiosRecentes.Count);

        // A serie soma o mesmo total dos cards (contagem agrupada por hora direto no SQLite).
        Assert.Equal(semana.EventosNoPeriodo, semana.SerieEventos.Sum(p => p.Quantidade));
        Assert.Equal(mes.EventosNoPeriodo, mes.SerieEventos.Sum(p => p.Quantidade));
        Assert.True(mes.EventosNoPeriodo > semana.EventosNoPeriodo);
        Assert.Equal(2, mes.SerieBloqueios.Sum(p => p.Quantidade));
    }

    [Fact]
    public async Task GranularidadeDaSerieAcompanhaOPeriodo()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();

        var hoje = await ObterAsync(ambiente, PeriodoDashboard.Hoje);
        var semana = await ObterAsync(ambiente, PeriodoDashboard.Dias7);
        var ano = await ObterAsync(ambiente, PeriodoDashboard.Dias360);

        Assert.Equal(GranularidadeSerie.Hora, hoje.Granularidade);
        Assert.Equal(13, hoje.SerieEventos.Count);
        Assert.Equal(GranularidadeSerie.Dia, semana.Granularidade);
        Assert.Equal(8, semana.SerieEventos.Count);
        Assert.Equal(GranularidadeSerie.Semana, ano.Granularidade);
        Assert.InRange(ano.SerieEventos.Count, 52, 53);
    }

    private static Task<IndicadoresDashboard> ObterAsync(AmbienteTeste ambiente, PeriodoDashboard periodo) =>
        ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoPainel>().ObterIndicadoresAsync(periodo));

    private static Task<int> GerarAtaqueAsync(AmbienteTeste ambiente, string ip, string url)
    {
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        var eventos = Enumerable.Range(0, 30)
            .Select(i => new EventoDetectado(EnderecoIp.Converter(ip), TipoFonte.LogIis, agora.AddSeconds(i), "GET", url, 404))
            .ToList();
        return ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos));
    }
}
