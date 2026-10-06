using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CRSP.IPS.Application;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRSP.IPS.Tests.Integracao;

/// <summary>Renderiza todas as telas autenticadas (pt-BR e en) com dados de exemplo, garantindo que nao ha erro de Razor.</summary>
public class PaginasWebTestes : IClassFixture<PaginasWebTestes.FabricaPainel>
{
    private readonly FabricaPainel _fabrica;

    public PaginasWebTestes(FabricaPainel fabrica) => _fabrica = fabrica;

    public static TheoryData<string, string> Paginas => new()
    {
        { "/", "Bloqueios ativos" },
        { "/Bloqueios", "Histórico e status dos IPs" },
        { "/Bloqueios?Busca=198.51.100.0/24&Status=Ativo", "value=\"198.51.100.0/24\"" },
        { "/Bloqueios/Detalhe?ip=203.0.113.7", "Histórico de bloqueios" },
        { "/Monitor", "Eventos suspeitos em tempo real" },
        { "/Monitor?x=1", "203.0.113.7" },
        { "/Endpoints", "Erros do servidor (5xx)" },
        { "/Endpoints?periodo=30d&site=teste", "Filtrar endpoint" },
        { "/Iis", "Application pool" },
        { "/Iis?periodo=7d&pool=x", "Application pool" },
        { "/?periodo=7d", "Ataques mitigados" },
        { "/?periodo=tudo", "IPs agressores mais frequentes" },
        { "/Regras", "Excesso de 404" },
        { "/Regras/Editar", "Limite de ocorrências" },
        { "/Regras/Editar?id=1", "Excesso de 404" },
        { "/Listas", "10.0.0.0/8" },
        { "/Listas?tipo=Negra", "198.51.100.0/24" },
        { "/Listas/Externas", "Spamhaus DROP" },
        { "/Listas/Externas?x=1", "Nova lista" },
        { "/Listas?tipo=Branca", "Baixar modelo CSV" },
        { "/Paises", "Política de países" },
        { "/Configuracoes", "Tempos de bloqueio progressivo" },
        { "/Usuarios", "Administradores do sistema" },
        { "/Usuarios/Novo", "Confirmar senha" },
        { "/Auditoria", "registro(s)" },
        { "/Conta/Senha", "Senha atual" }
    };

    [Theory]
    [MemberData(nameof(Paginas))]
    public async Task PaginaRenderizaEmPortugues(string url, string textoEsperado)
    {
        var resposta = await _fabrica.CriarCliente("pt-BR").GetAsync(url);
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.True(resposta.StatusCode == HttpStatusCode.OK, $"{url}: {(int)resposta.StatusCode}\n{html[..Math.Min(html.Length, 2000)]}");
        AssertContemTexto(url, textoEsperado, html);
        _fabrica.SalvarAmostra(url, html);
    }

    [Theory]
    [InlineData("/", "Active blocks")]
    [InlineData("/Bloqueios", "Block IP")]
    [InlineData("/Endpoints", "Server errors (5xx)")]
    [InlineData("/Iis", "Application pool")]
    [InlineData("/Paises", "Country policy")]
    [InlineData("/Configuracoes", "Simulation mode")]
    public async Task PaginaRenderizaEmIngles(string url, string textoEsperado)
    {
        var resposta = await _fabrica.CriarCliente("en").GetAsync(url);
        var html = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        AssertContemTexto(url, textoEsperado, html);
    }

    [Fact]
    public async Task PaginasExigemAutenticacao()
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resposta = await cliente.GetAsync("/Bloqueios");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Contains("/Conta/Entrar", resposta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task TelaDeLoginRenderizaSemAutenticacao()
    {
        _fabrica.CriarCliente("pt-BR");
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resposta = await cliente.GetAsync("/Conta/Entrar");
        var html = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("E-mail", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task ModeloCsvDasListasEhBaixado()
    {
        var resposta = await _fabrica.CriarCliente("pt-BR").GetAsync("/Listas/modelo.csv");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("faixa;descricao", (await resposta.Content.ReadAsStringAsync()).TrimStart('﻿'));
    }

    [Theory]
    [InlineData("/Bloqueios/exportar.csv", "IP;País;Cidade", "198.51.100.77;")]
    [InlineData("/Bloqueios/exportar.csv?Status=Ativo&Busca=198.51.100.77", "IP;País;Cidade", "Varredura observada")]
    [InlineData("/Endpoints/exportar.csv?periodo=30d&ordem=5xx", "Site;Método;Endpoint;Total;Sem erro;4xx;5xx", null)]
    [InlineData("/Iis/sites.csv?periodo=7d&ordem=memmax&crescente=true", "Site;Application pool;Acessos", null)]
    public async Task TabelasSaoExportadasEmCsv(string url, string cabecalho, string? conteudo)
    {
        var resposta = await _fabrica.CriarCliente("pt-BR").GetAsync(url);
        var csv = (await resposta.Content.ReadAsStringAsync()).TrimStart('﻿');

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".csv", resposta.Content.Headers.ContentDisposition?.FileNameStar ?? resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.StartsWith(cabecalho, csv);
        if (conteudo is not null)
            Assert.Contains(conteudo, csv);
    }

    [Fact]
    public async Task ExportacaoDeBloqueiosRespeitaOsFiltros()
    {
        var resposta = await _fabrica.CriarCliente("pt-BR").GetAsync("/Bloqueios/exportar.csv?Busca=192.0.2.1");
        var linhas = (await resposta.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(linhas);
    }

    [Fact]
    public async Task ExportacaoCsvExigeAutenticacao()
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resposta = await cliente.GetAsync("/Bloqueios/exportar.csv");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }

    [Theory]
    [InlineData("203.0.113.8", true)]
    [InlineData("203.0.113.", true)]
    [InlineData("192.0.2.", false)]
    public async Task MonitorFiltraEventosPorIp(string ip, bool haEventos)
    {
        _fabrica.CriarCliente("pt-BR");
        using var escopo = _fabrica.Services.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoPainel>();

        var todos = await servico.ListarEventosRecentesAsync(new FiltroEventos(), null);
        var eventos = await servico.ListarEventosRecentesAsync(new FiltroEventos(Ip: ip), null);

        Assert.Equal(haEventos, eventos.Count > 0);
        Assert.Equal(todos.Count(e => e.Ip.StartsWith(ip)), eventos.Count);
        Assert.All(eventos, e => Assert.StartsWith(ip, e.Ip));
    }

    [Fact]
    public void TabelasDeMetricasOrdenamPelaColunaEscolhida()
    {
        LinhaRequisicoes Linha(string endpoint, int total, int erro5xx, long tempo) => new("site", "GET", endpoint, total, total - erro5xx, 0, 0, erro5xx, tempo, 0);
        LinhaRequisicoes[] linhas = [Linha("/a", 10, 1, 1000), Linha("/b", 5, 4, 100), Linha("/c", 20, 0, 60000)];

        Assert.Equal(["/c", "/a", "/b"], Web.Infra.OrdenacaoMetricas.Ordenar(linhas, "total", true).Select(l => l.Endpoint));
        Assert.Equal(["/b", "/a", "/c"], Web.Infra.OrdenacaoMetricas.Ordenar(linhas, "erro", true).Select(l => l.Endpoint));
        Assert.Equal(["/b", "/a", "/c"], Web.Infra.OrdenacaoMetricas.Ordenar(linhas, "media", false).Select(l => l.Endpoint));

        var consumo = new ResumoPool("Pool", 1, 300, 2, 1, 1, 1, DateTime.UtcNow);
        var totais = new TotaisRequisicoes(0, 0, 0, 0, 0, 0, 0);
        ResumoSiteIis[] sites = [new("sem-pool", null, totais, null), new("grande", "Pool", totais, consumo), new("pequeno", "Outro", totais, consumo with { MemoriaMaxima = 100 })];

        // Site sem amostra do pool fica no fim nas duas direcoes.
        Assert.Equal(["grande", "pequeno", "sem-pool"], Web.Infra.OrdenacaoMetricas.Ordenar(sites, "memmax", true).Select(s => s.Site));
        Assert.Equal(["pequeno", "grande", "sem-pool"], Web.Infra.OrdenacaoMetricas.Ordenar(sites, "memmax", false).Select(s => s.Site));
    }

    [Fact]
    public async Task RespostasTrazemCabecalhosDeSeguranca()
    {
        var resposta = await _fabrica.CriarCliente("pt-BR").GetAsync("/");

        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("script-src 'self'", resposta.Headers.GetValues("Content-Security-Policy").Single());
    }

    /// <summary>Em caso de falha mostra o conteudo principal da pagina (facilita diagnosticar falhas intermitentes).</summary>
    private static void AssertContemTexto(string url, string textoEsperado, string html)
    {
        var texto = WebUtility.HtmlDecode(html);
        if (texto.Contains(textoEsperado))
            return;

        var inicio = Math.Max(0, texto.IndexOf("<main", StringComparison.Ordinal));
        Assert.Fail($"{url}: texto \"{textoEsperado}\" nao encontrado. Trecho:\n{texto.Substring(inicio, Math.Min(3000, texto.Length - inicio))}");
    }

    public sealed class FabricaPainel : WebApplicationFactory<Program>
    {
        private readonly string _caminhoBanco = Path.Combine(Path.GetTempPath(), $"crspips-web-{Guid.NewGuid():N}.db");
        private bool _populado;

        public string PastaAmostras { get; } = Path.Combine(Path.GetTempPath(), "crspips-amostras");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("CRSPIPS:CaminhoBanco", _caminhoBanco);
            builder.UseSetting("CRSPIPS:PastaGeoIp", Path.GetTempPath());
            builder.ConfigureTestServices(servicos =>
            {
                servicos.AddAuthentication(AutenticacaoTeste.Esquema)
                    .AddScheme<AuthenticationSchemeOptions, AutenticacaoTeste>(AutenticacaoTeste.Esquema, _ => { });
                servicos.AdicionarMotor();
                servicos.AddSingleton<IServicoFirewall, FirewallFalso>();
                servicos.AddSingleton<IAtualizadorBaseGeo, AtualizadorGeoFalso>();
                servicos.AddSingleton<IBaixadorListasExternas, BaixadorListasFalso>();
                var geo = new GeolocalizacaoFalsa();
                geo.Paises["203.0.113.7"] = "CN";
                geo.Paises["203.0.113.8"] = "RU";
                geo.Paises["198.51.100.77"] = "US";
                servicos.AddSingleton<IServicoGeolocalizacao>(geo);
            });
        }

        public HttpClient CriarCliente(string cultura)
        {
            PopularUmaVez();
            var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            cliente.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c%3D{cultura}%7Cuic%3D{cultura}");
            cliente.DefaultRequestHeaders.Add(AutenticacaoTeste.Cabecalho, "1");
            return cliente;
        }

        public void SalvarAmostra(string url, string html)
        {
            Directory.CreateDirectory(PastaAmostras);
            var nome = string.Concat(url.Split('?')[0].Trim('/').Replace('/', '-')) is { Length: > 0 } n ? n : "dashboard";
            if (!url.Contains('?'))
                File.WriteAllText(Path.Combine(PastaAmostras, $"{nome}.html"), html);
        }

        private void PopularUmaVez()
        {
            if (_populado)
                return;
            _populado = true;

            using var escopo = Services.CreateScope();
            var provedor = escopo.ServiceProvider;
            provedor.GetRequiredService<ServicoUsuarios>().CriarAsync("Administrador", "admin@teste.local", "SenhaForte123").GetAwaiter().GetResult();

            var bloqueios = provedor.GetRequiredService<ServicoBloqueios>();
            bloqueios.BloquearManualAsync("198.51.100.77", TimeSpan.FromHours(24), "Varredura observada").GetAwaiter().GetResult();
            provedor.GetRequiredService<ServicoListas>().AdicionarAsync(TipoLista.Negra, "198.51.100.0/24", "Rede hostil").GetAwaiter().GetResult();

            var agora = DateTime.UtcNow;
            var eventos = Enumerable.Range(0, 35)
                .Select(i => new EventoDetectado(EnderecoIp.Converter("203.0.113.7"), TipoFonte.LogIis, agora.AddSeconds(i - 60), "GET", i % 2 == 0 ? "/wp-login.php" : $"/x{i}", 404, "curl/8.0"))
                .Concat(Enumerable.Range(0, 6).Select(i => new EventoDetectado(EnderecoIp.Converter("203.0.113.8"), TipoFonte.EventoWindows, agora.AddSeconds(i - 30), IdEventoWindows: 4625, Detalhe: "Security | administrator")))
                .ToList();
            provedor.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos).GetAwaiter().GetResult();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            foreach (var arquivo in new[] { _caminhoBanco, _caminhoBanco + "-wal", _caminhoBanco + "-shm" })
            {
                if (File.Exists(arquivo))
                    File.Delete(arquivo);
            }
        }
    }

    private sealed class AutenticacaoTeste(IOptionsMonitor<AuthenticationSchemeOptions> opcoes, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(opcoes, logger, encoder)
    {
        public const string Esquema = "Teste";
        public const string Cabecalho = "X-Teste-Usuario";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(Cabecalho))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identidade = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "Administrador"),
                new Claim(ClaimTypes.Email, "admin@teste.local")
            ], Esquema);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Redirect("/Conta/Entrar");
            return Task.CompletedTask;
        }
    }
}
