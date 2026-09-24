using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Infrastructure.Fontes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CRSP.IPS.Tests.Integracao;

public class SitesERankingTestes
{
    private const string Campos =
        "#Fields: date time s-ip cs-method cs-uri-stem cs-uri-query s-port cs-username c-ip cs(User-Agent) cs(Referer) sc-status sc-substatus sc-win32-status time-taken";
    private const string Linha =
        "2026-09-23 14:05:09 10.0.0.5 GET /img/site.webmanifest - 443 - 200.195.250.42 Mozilla/5.0 - 404 0 2 15";

    [Fact]
    public void Site_EResolvidoPelaPastaDoLogEPelaConfiguracaoDoIis()
    {
        var config = Path.Combine(Path.GetTempPath(), $"crspips-apphost-{Guid.NewGuid():N}.config");
        File.WriteAllText(config, """
            <configuration><system.applicationHost><sites>
              <site name="ips.exemplo.com.br" id="14"><bindings /></site>
              <site name="apimobile.exemplo.com.br" id="3" />
            </sites></system.applicationHost></configuration>
            """);
        try
        {
            var resolvedor = new ResolvedorSitesIis(NullLogger<ResolvedorSitesIis>.Instance, config);
            var analisador = AnalisadorW3C.DeDiretivaCampos(Campos)!;

            Assert.Equal("14", AnalisadorW3C.ExtrairIdSite("W3SVC14"));
            Assert.Null(AnalisadorW3C.ExtrairIdSite("logs"));
            Assert.Equal("ips.exemplo.com.br", analisador.Interpretar(Linha, TipoFonte.LogIis, "14", resolvedor.ObterNome)!.Site);
            Assert.Equal("Site 99", analisador.Interpretar(Linha, TipoFonte.LogIis, "99", resolvedor.ObterNome)!.Site);
            Assert.Null(analisador.Interpretar(Linha, TipoFonte.LogIis)!.Site);
        }
        finally
        {
            File.Delete(config);
        }
    }

    [Fact]
    public void Site_UsaOHostDaRequisicaoQuandoOLogRegistraCsHost()
    {
        var analisador = AnalisadorW3C.DeDiretivaCampos(Campos + " cs-host")!;

        var evento = analisador.Interpretar(Linha + " ips.exemplo.com.br", TipoFonte.LogIis, "14", _ => "Outro nome");

        Assert.Equal("ips.exemplo.com.br", evento!.Site);
    }

    [Fact]
    public async Task BloqueioSimulado_ContinuaRegistrandoEventosSemCriarNovoBloqueio()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var inicio = ambiente.Relogio.GetUtcNow().UtcDateTime;

        Assert.Equal(1, await ProcessarAsync(ambiente, Gerar404("203.0.113.7", 30, inicio)));
        var eventosAposBloqueio = await ContarEventosAsync(ambiente, "203.0.113.7");

        Assert.Equal(0, await ProcessarAsync(ambiente, Gerar404("203.0.113.7", 30, inicio.AddMinutes(90))));

        Assert.True(await ContarEventosAsync(ambiente, "203.0.113.7") > eventosAposBloqueio);
        var ativos = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ListarAtivosAsync());
        Assert.Single(ativos, b => b.Ip == "203.0.113.7");
    }

    [Fact]
    public async Task PoliticaSomenteSuspeitos_BloqueiaEstrangeiroSoNaPrimeiraTentativaSuspeita()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.Geolocalizacao.Paises["192.0.2.50"] = "BR";
        ambiente.Geolocalizacao.Paises["203.0.113.7"] = "US";
        ambiente.Geolocalizacao.Paises["198.51.100.9"] = "US";
        ambiente.Geolocalizacao.Paises["203.0.113.80"] = "BR";

        var salvar = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoConfiguracao>().SalvarPoliticaPaisesAsync(
            new DadosPoliticaPaises(ModoPoliticaPaises.PermitirSomenteListados, AplicacaoPoliticaPaises.ReativaSuspeitos, ["BR"], [])));
        Assert.True(salvar.Sucesso, salvar.Erro);

        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        var visitaNormalEstrangeira = new EventoDetectado(EnderecoIp.Converter("198.51.100.9"), TipoFonte.LogIis, agora, "GET", "/", 200);
        var varreduraEstrangeira = new EventoDetectado(EnderecoIp.Converter("203.0.113.7"), TipoFonte.LogIis, agora, "GET", "/wp-login.php", 404);
        var varreduraBrasileira = varreduraEstrangeira with { Ip = EnderecoIp.Converter("203.0.113.80") };

        Assert.Equal(1, await ProcessarAsync(ambiente, [visitaNormalEstrangeira, varreduraEstrangeira, varreduraBrasileira]));

        var bloqueio = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync("203.0.113.7"));
        Assert.Equal(OrigemBloqueio.Pais, bloqueio!.Origem);
        Assert.NotNull(bloqueio.RegraId);
        Assert.Null(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync("198.51.100.9")));
        Assert.Null(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync("203.0.113.80")));
    }

    [Fact]
    public async Task Ranking_OrdenaPelosEventosRegistradosENaoPeloGatilho()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        await ProcessarAsync(ambiente, Gerar404("203.0.113.7", 30, agora));
        var urls = Enumerable.Range(0, 3)
            .Select(i => new EventoDetectado(EnderecoIp.Converter("198.51.100.23"), TipoFonte.LogIis, agora.AddSeconds(i), "GET", "/.env", 404))
            .ToList();
        await ProcessarAsync(ambiente, urls);

        var indicadores = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoPainel>().ObterIndicadoresAsync());

        Assert.Equal("203.0.113.7", indicadores.TopIps[0].Rotulo);
        Assert.Equal(20, indicadores.TopIps[0].Quantidade);
        Assert.StartsWith("1|", indicadores.TopIps[0].Complemento);
    }

    private static List<EventoDetectado> Gerar404(string ip, int quantidade, DateTime inicio) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new EventoDetectado(EnderecoIp.Converter(ip), TipoFonte.LogIis, inicio.AddSeconds(i), "GET", $"/pagina-{i}", 404))
            .ToList();

    private static Task<int> ProcessarAsync(AmbienteTeste ambiente, IReadOnlyList<EventoDetectado> eventos) =>
        ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos));

    private static Task<int> ContarEventosAsync(AmbienteTeste ambiente, string ip) =>
        ambiente.ExecutarAsync(async p => (await p.GetRequiredService<IRepositorioEventos>().ListarPorIpAsync(ip, 500)).Count);
}
