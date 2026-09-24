using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Motor;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Infrastructure;
using Donc.IPS.Infrastructure.Geolocalizacao;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Donc.IPS.Tests.Integracao;

public class GeoIpTestes
{
    [Fact]
    public void ConfiguracaoGeoIp_VerificaQuandoSolicitadaOuVencida()
    {
        var agora = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        var configuracao = new ConfiguracaoGeoIp();
        Assert.False(configuracao.DeveVerificar(agora));
        Assert.Throws<ArgumentException>(() => configuracao.AlterarCredenciais("abc", "x", "1234", true));

        configuracao.AlterarCredenciais("123456", "protegida", "1234", atualizacaoAutomatica: true);
        Assert.True(configuracao.DeveVerificar(agora));

        configuracao.RegistrarVerificacao(agora, sucesso: true, basesAtualizadas: 3, "ok");
        Assert.False(configuracao.DeveVerificar(agora.AddHours(23)));
        Assert.True(configuracao.DeveVerificar(agora.AddHours(24)));

        configuracao.SolicitarAtualizacao();
        Assert.True(configuracao.DeveVerificar(agora.AddMinutes(1)));
    }

    [Fact]
    public async Task Painel_GuardaChaveProtegidaEMotorUsaChaveOriginal()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();

        var salvar = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoGeoIp>().SalvarCredenciaisAsync("123456", "chave-secreta-ABCD", true));
        Assert.True(salvar.Sucesso, salvar.Erro);

        var configuracao = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoGeoIp>().ObterAsync());
        Assert.DoesNotContain("chave-secreta", configuracao.ChaveProtegida);
        Assert.Equal("ABCD", configuracao.FinalChave);
        Assert.True(configuracao.AtualizacaoSolicitada);

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoAtualizacaoGeo>().ExecutarSeNecessarioAsync());

        Assert.Equal(("123456", "chave-secreta-ABCD", true), ambiente.AtualizadorGeo.UltimaChamada);
        var depois = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoGeoIp>().ObterAsync());
        Assert.False(depois.AtualizacaoSolicitada);
        Assert.True(depois.UltimoResultadoSucesso);

        var manterChave = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoGeoIp>().SalvarCredenciaisAsync("654321", null, false));
        Assert.True(manterChave.Sucesso);
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoAtualizacaoGeo>().ExecutarSeNecessarioAsync());
        Assert.Equal(("654321", "chave-secreta-ABCD", true), ambiente.AtualizadorGeo.UltimaChamada);
    }

    [Fact]
    public async Task Atualizador_SegueRedirecionamentoSemCredencialEConfereHash()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"crspips-geo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);
        try
        {
            File.WriteAllText(Path.Combine(pasta, "GeoLite2-City.mmdb"), "recente");
            File.WriteAllText(Path.Combine(pasta, "GeoLite2-ASN.mmdb"), "recente");

            var zip = CriarZipPaises();
            var handler = new HandlerMaxMindFalso(zip, Convert.ToHexStringLower(SHA256.HashData(zip)));
            var atualizador = new AtualizadorBaseGeoMaxMind(
                new FabricaHttpFixa(handler),
                Options.Create(new OpcoesCrspips { PastaGeoIp = pasta }),
                NullLogger<AtualizadorBaseGeoMaxMind>.Instance);

            var resultado = await atualizador.AtualizarAsync("123456", "chave", forcar: false, CancellationToken.None);

            Assert.True(resultado.Sucesso, resultado.Mensagem);
            Assert.Equal(1, resultado.BasesAtualizadas);
            Assert.True(File.Exists(Path.Combine(pasta, "GeoLite2-Country-Blocks-IPv4.csv")));
            Assert.True(File.Exists(Path.Combine(pasta, "GeoLite2-Country-Locations-en.csv")));
            Assert.False(Directory.Exists(Path.Combine(pasta, ".download")));
            Assert.All(handler.Requisicoes.Where(r => r.Host == "download.maxmind.com"), r => Assert.True(r.ComCredencial));
            Assert.All(handler.Requisicoes.Where(r => r.Host != "download.maxmind.com"), r => Assert.False(r.ComCredencial));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Atualizador_RecusaPacoteComHashDivergente()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"crspips-geo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);
        try
        {
            File.WriteAllText(Path.Combine(pasta, "GeoLite2-City.mmdb"), "recente");
            File.WriteAllText(Path.Combine(pasta, "GeoLite2-ASN.mmdb"), "recente");
            var atualizador = new AtualizadorBaseGeoMaxMind(
                new FabricaHttpFixa(new HandlerMaxMindFalso(CriarZipPaises(), new string('0', 64))),
                Options.Create(new OpcoesCrspips { PastaGeoIp = pasta }),
                NullLogger<AtualizadorBaseGeoMaxMind>.Instance);

            var resultado = await atualizador.AtualizarAsync("123456", "chave", forcar: false, CancellationToken.None);

            Assert.False(resultado.Sucesso);
            Assert.False(File.Exists(Path.Combine(pasta, "GeoLite2-Country-Blocks-IPv4.csv")));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [Fact]
    public async Task Atualizador_InformaCredencialInvalida()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"crspips-geo-{Guid.NewGuid():N}");
        try
        {
            var atualizador = new AtualizadorBaseGeoMaxMind(
                new FabricaHttpFixa(new HandlerMaxMindFalso([], "", statusDownload: HttpStatusCode.Unauthorized)),
                Options.Create(new OpcoesCrspips { PastaGeoIp = pasta }),
                NullLogger<AtualizadorBaseGeoMaxMind>.Instance);

            var resultado = await atualizador.AtualizarAsync("123456", "errada", forcar: true, CancellationToken.None);

            Assert.False(resultado.Sucesso);
            Assert.StartsWith("Credenciais MaxMind inválidas", resultado.Mensagem);
        }
        finally
        {
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
    }

    private static byte[] CriarZipPaises()
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Adicionar(string nome, string conteudo)
            {
                using var escrita = new StreamWriter(zip.CreateEntry($"GeoLite2-Country-CSV_20260922/{nome}").Open(), Encoding.UTF8);
                escrita.Write(conteudo);
            }

            Adicionar("GeoLite2-Country-Blocks-IPv4.csv", "network,geoname_id,registered_country_geoname_id\n200.0.0.0/8,3469034,3469034\n");
            Adicionar("GeoLite2-Country-Blocks-IPv6.csv", "network,geoname_id,registered_country_geoname_id\n");
            Adicionar("GeoLite2-Country-Locations-en.csv", "geoname_id,locale_code,continent_code,continent_name,country_iso_code\n3469034,en,SA,South America,BR\n");
        }

        return memoria.ToArray();
    }

    private sealed class FabricaHttpFixa(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Simula a API da MaxMind: responde 302 para um armazenamento externo, como a real.</summary>
    private sealed class HandlerMaxMindFalso(byte[] zip, string hash, HttpStatusCode statusDownload = HttpStatusCode.Found) : HttpMessageHandler
    {
        public List<(string Host, bool ComCredencial)> Requisicoes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
        {
            var uri = requisicao.RequestUri!;
            Requisicoes.Add((uri.Host, requisicao.Headers.Authorization is not null));

            if (uri.Host == "download.maxmind.com")
            {
                if (statusDownload != HttpStatusCode.Found)
                    return Task.FromResult(new HttpResponseMessage(statusDownload));
                var resposta = new HttpResponseMessage(HttpStatusCode.Found);
                resposta.Headers.Location = new Uri($"https://armazenamento.exemplo{uri.PathAndQuery}");
                return Task.FromResult(resposta);
            }

            var conteudo = uri.Query.EndsWith(".sha256")
                ? new ByteArrayContent(Encoding.ASCII.GetBytes($"{hash}  GeoLite2-Country-CSV_20260922.zip\n"))
                : new ByteArrayContent(zip);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = conteudo });
        }
    }
}
