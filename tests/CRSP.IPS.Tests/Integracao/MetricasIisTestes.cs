using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Infrastructure.Fontes;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class MetricasIisTestes
{
    private static EventoDetectado Requisicao(DateTime momento, string site, string metodo, string url, int status, int? tempoMs = 10) =>
        new(EnderecoIp.Converter("203.0.113.7"), TipoFonte.LogIis, momento, metodo, url, status, Site: site, TempoMs: tempoMs);

    [Theory]
    [InlineData("/api/os/123", "/api/os/{id}")]
    [InlineData("/API/Os/123?x=1&y=2", "/api/os/{id}")]
    [InlineData("/api/os/3f2504e0-4f89-11d3-9a0c-0305e82c3301/itens", "/api/os/{id}/itens")]
    [InlineData("/api/token/9f86d081884c7d659a2feaa0c55ad015", "/api/token/{id}")]
    [InlineData("/", "/")]
    [InlineData("", "/")]
    [InlineData(null, "/")]
    [InlineData("/Content/site.css?v=3", NormalizadorEndpoint.Estaticos)]
    [InlineData("/img/logo.PNG", NormalizadorEndpoint.Estaticos)]
    [InlineData("/Admin/Home/Index/", "/admin/home/index")]
    public void NormalizadorEndpoint_AgrupaIdentificadores(string? url, string esperado) =>
        Assert.Equal(esperado, NormalizadorEndpoint.Normalizar(url));

    [Fact]
    public void AnalisadorW3C_LeTempoDaRequisicao()
    {
        var analisador = AnalisadorW3C.DeDiretivaCampos("#Fields: date time s-sitename cs-method cs-uri-stem c-ip sc-status time-taken")!;

        var evento = analisador.Interpretar("2026-10-01 12:00:05 W3SVC2 GET /api/os 203.0.113.7 500 1234", TipoFonte.LogIis);

        Assert.NotNull(evento);
        Assert.Equal(1234, evento.TempoMs);
        Assert.Equal(500, evento.CodigoStatus);
    }

    [Fact]
    public async Task Requisicoes_SaoAgregadasPorEndpointESomadasEntreCiclos()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        List<EventoDetectado> primeiroCiclo =
        [
            Requisicao(agora, "api.cliente.com.br", "GET", "/api/os/1", 200, 100),
            Requisicao(agora, "api.cliente.com.br", "GET", "/api/os/2", 200, 300),
            Requisicao(agora, "api.cliente.com.br", "GET", "/api/os/3", 500, 900),
            Requisicao(agora, "api.cliente.com.br", "POST", "/api/os", 404),
            Requisicao(agora, "api.cliente.com.br", "GET", "/Content/a.css", 200),
            new(EnderecoIp.Converter("203.0.113.7"), TipoFonte.HttpErr, agora, Url: "/ignorado", CodigoStatus: 503, Site: "api.cliente.com.br")
        ];

        Assert.Equal(5, await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarRequisicoesAsync(primeiroCiclo)));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>()
            .RegistrarRequisicoesAsync([Requisicao(agora.AddMinutes(5), "api.cliente.com.br", "GET", "/api/os/9", 200, 50)]));

        var painel = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasPainel>().ObterEndpointsAsync(PeriodoDashboard.Hoje, null));

        Assert.True(painel.HaDados);
        Assert.Equal(6, painel.Totais.Total);
        Assert.Equal(1, painel.Totais.ErroServidor);
        Assert.Equal(1, painel.Totais.ErroCliente);
        Assert.Equal(4, painel.Totais.Sucesso);
        Assert.Equal(900, painel.Totais.TempoMaximoMs);

        var os = Assert.Single(painel.Endpoints, e => e.Endpoint == "/api/os/{id}" && e.Metodo == "GET");
        Assert.Equal(4, os.Total);
        Assert.Equal(1, os.ErroServidor);
        Assert.Equal(25, os.PercentualErro);
        Assert.Equal((100 + 300 + 900 + 50) / 4d, os.TempoMedioMs);
        Assert.Contains(painel.Endpoints, e => e.Endpoint == NormalizadorEndpoint.Estaticos);
        Assert.DoesNotContain(painel.Endpoints, e => e.Endpoint == "/ignorado");
        Assert.Equal(["api.cliente.com.br"], painel.Sites);
    }

    [Fact]
    public async Task Requisicoes_FiltramPorSite()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarRequisicoesAsync(
        [
            Requisicao(agora, "admin.cliente.com.br", "GET", "/home", 200),
            Requisicao(agora, "api.cliente.com.br", "GET", "/api/os", 200),
            Requisicao(agora, "api.cliente.com.br", "GET", "/api/os", 500)
        ]));

        var painel = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasPainel>().ObterEndpointsAsync(PeriodoDashboard.Hoje, "api.cliente.com.br"));

        Assert.Equal(["admin.cliente.com.br", "api.cliente.com.br"], painel.Sites);
        Assert.Equal(2, painel.Totais.Total);
        Assert.Equal(2, painel.Serie.Sum(p => p.Sucesso + p.ErroServidor));
        Assert.Equal("/api/os", Assert.Single(painel.Endpoints).Endpoint);
    }

    [Fact]
    public async Task Processos_GuardamMinimoMaximoEMediaPorPool()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        const long Mb = 1024 * 1024;
        IReadOnlyList<SitePool> sites = [new("api.cliente.com.br", "PoolApi"), new("sem-trafego.com.br", "PoolApi")];

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarProcessosAsync(
            [new("PoolApi", 100, 20 * Mb, 10), new("PoolApi", 101, 30 * Mb, 20)], sites));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarProcessosAsync(
            [new("PoolApi", 100, 2000 * Mb, 40)], sites));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarRequisicoesAsync(
            [Requisicao(ambiente.Relogio.GetUtcNow().UtcDateTime, "api.cliente.com.br", "GET", "/api/os", 200)]));

        var painel = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasPainel>().ObterIisAsync(PeriodoDashboard.Hoje, null));

        var pool = Assert.Single(painel.Pools);
        Assert.Equal("PoolApi", pool.Pool);
        Assert.Equal(50 * Mb, pool.MemoriaMinima);
        Assert.Equal(2000 * Mb, pool.MemoriaMaxima);
        Assert.Equal((50 + 2000) * Mb / 2, pool.MemoriaMedia);
        Assert.Equal(2, pool.ProcessosMaximo);
        Assert.Equal(40, pool.CpuMaxima);
        Assert.Equal("PoolApi", painel.PoolSelecionado);
        Assert.Single(painel.SerieMemoria);

        var api = Assert.Single(painel.Sites, s => s.Site == "api.cliente.com.br");
        Assert.Equal(1, api.Requisicoes.Total);
        Assert.Equal(2000 * Mb, api.Consumo?.MemoriaMaxima);
        Assert.Contains(painel.Sites, s => s.Site == "sem-trafego.com.br" && s.Requisicoes.Total == 0);
    }

    [Fact]
    public async Task Retencao_RemoveMetricasAntigas()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarRequisicoesAsync(
        [
            Requisicao(agora.AddDays(-100), "api", "GET", "/antigo", 200),
            Requisicao(agora, "api", "GET", "/novo", 200)
        ]));

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().AplicarRetencaoAsync());

        var painel = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasPainel>().ObterEndpointsAsync(PeriodoDashboard.Tudo, null));
        Assert.Equal("/novo", Assert.Single(painel.Endpoints).Endpoint);
    }

    [Fact]
    public async Task Retencao_UsaOsPrazosDaConfiguracao()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        await ambiente.ExecutarAsync(async p =>
        {
            var configuracao = await p.GetRequiredService<IRepositorioConfiguracao>().ObterAsync();
            Assert.Equal(90, configuracao.RetencaoMetricasEndpointsDias);
            Assert.Equal(30, configuracao.RetencaoMetricasProcessosDias);
            configuracao.AlterarRetencaoMetricas(7, 2, agora, "teste");
            await p.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync();
        });
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().RegistrarRequisicoesAsync(
        [
            Requisicao(agora.AddDays(-10), "api", "GET", "/antigo", 200),
            Requisicao(agora.AddDays(-3), "api", "GET", "/recente", 200)
        ]));

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasIis>().AplicarRetencaoAsync());

        var painel = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoMetricasPainel>().ObterEndpointsAsync(PeriodoDashboard.Tudo, null));
        Assert.Equal("/recente", Assert.Single(painel.Endpoints).Endpoint);
    }

    [Fact]
    public void Configuracao_RejeitaRetencaoDeMetricasForaDoIntervalo()
    {
        var configuracao = new Configuracao();

        Assert.Throws<ArgumentException>(() => configuracao.AlterarRetencaoMetricas(0, 30, DateTime.UtcNow, "teste"));
        Assert.Throws<ArgumentException>(() => configuracao.AlterarRetencaoMetricas(90, 366, DateTime.UtcNow, "teste"));
    }
}
