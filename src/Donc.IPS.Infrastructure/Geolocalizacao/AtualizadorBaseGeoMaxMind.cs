using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using MaxMind.Db;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Donc.IPS.Infrastructure.Geolocalizacao;

/// <summary>
/// Baixa as bases GeoLite2 pela API de download da MaxMind (autenticacao Basic: ID da conta + chave de licenca),
/// confere o SHA-256, extrai e substitui os arquivos na pasta GeoIP.
/// A MaxMind responde com redirecionamento para um armazenamento externo: o redirecionamento e seguido
/// manualmente e SEM o cabecalho de autenticacao, para a credencial nunca sair do dominio da MaxMind.
/// </summary>
internal sealed class AtualizadorBaseGeoMaxMind(
    IHttpClientFactory fabricaHttp,
    IOptions<OpcoesCrspips> opcoes,
    ILogger<AtualizadorBaseGeoMaxMind> logger) : IAtualizadorBaseGeo
{
    public const string NomeClienteHttp = "MaxMind";
    private const string UrlBase = "https://download.maxmind.com/geoip/databases/";
    private static readonly TimeSpan IdadeMaximaBase = TimeSpan.FromDays(3);

    private static readonly EdicaoGeo[] Edicoes =
    [
        new("GeoLite2-City", "tar.gz", [LocalizadorArquivosGeo.ArquivoCidade]),
        new("GeoLite2-ASN", "tar.gz", [LocalizadorArquivosGeo.ArquivoAsn]),
        new("GeoLite2-Country-CSV", "zip",
            [LocalizadorArquivosGeo.ArquivoBlocosIPv4, LocalizadorArquivosGeo.ArquivoBlocosIPv6, LocalizadorArquivosGeo.ArquivoLocais])
    ];

    public async Task<ResultadoAtualizacaoGeo> AtualizarAsync(string contaId, string chaveLicenca, bool forcar, CancellationToken ct)
    {
        var pasta = opcoes.Value.PastaGeoIp;
        var pastaTemporaria = Path.Combine(pasta, LocalizadorArquivosGeo.PastaTemporaria);
        Directory.CreateDirectory(pastaTemporaria);

        var credencial = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{contaId}:{chaveLicenca}")));
        var cliente = fabricaHttp.CreateClient(NomeClienteHttp);
        var atualizadas = 0;

        try
        {
            foreach (var edicao in Edicoes)
            {
                var principal = Path.Combine(pasta, edicao.Arquivos[0]);
                if (!forcar && File.Exists(principal) && DateTime.UtcNow - File.GetLastWriteTimeUtc(principal) < IdadeMaximaBase)
                    continue;

                await BaixarEInstalarAsync(cliente, credencial, edicao, pasta, pastaTemporaria, ct);
                atualizadas++;
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ResultadoAtualizacaoGeo(false, atualizadas, "Credenciais MaxMind inválidas: confira o ID da conta e a chave de licença.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new ResultadoAtualizacaoGeo(false, atualizadas, "Limite diário de downloads da MaxMind atingido. Tente novamente amanhã.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or InvalidDatabaseException)
        {
            logger.LogError(ex, "Falha ao atualizar as bases GeoIP");
            return new ResultadoAtualizacaoGeo(false, atualizadas, "Falha ao baixar as bases GeoIP. Verifique o acesso do servidor a download.maxmind.com e o log do serviço.");
        }
        finally
        {
            TentarApagar(pastaTemporaria);
        }

        return new ResultadoAtualizacaoGeo(true, atualizadas,
            atualizadas == 0 ? "Bases GeoIP já estão atualizadas." : "Bases GeoIP atualizadas com sucesso.");
    }

    private async Task BaixarEInstalarAsync(
        HttpClient cliente, AuthenticationHeaderValue credencial, EdicaoGeo edicao, string pasta, string pastaTemporaria, CancellationToken ct)
    {
        var url = $"{UrlBase}{edicao.Id}/download?suffix={edicao.Sufixo}";
        var pacote = Path.Combine(pastaTemporaria, $"{edicao.Id}.{edicao.Sufixo}");

        await BaixarParaArquivoAsync(cliente, credencial, url, pacote, ct);
        await ConferirHashAsync(cliente, credencial, $"{url}.sha256", pacote, ct);

        var extraidos = edicao.Sufixo == "zip"
            ? ExtrairZip(pacote, edicao.Arquivos, pastaTemporaria)
            : await ExtrairTarGzAsync(pacote, edicao.Arquivos, pastaTemporaria, ct);

        var faltando = edicao.Arquivos.Except(extraidos.Keys).ToList();
        if (faltando.Count > 0)
            throw new InvalidDataException($"Pacote {edicao.Id} sem os arquivos: {string.Join(", ", faltando)}");

        foreach (var (nome, temporario) in extraidos)
        {
            if (nome.EndsWith(".mmdb", StringComparison.OrdinalIgnoreCase))
            {
                using var validacao = new DatabaseReader(temporario, FileAccessMode.Memory);
            }

            var destino = Path.Combine(pasta, nome);
            File.Move(temporario, destino, overwrite: true);
            File.SetLastWriteTimeUtc(destino, DateTime.UtcNow);
        }

        logger.LogInformation("Base {Edicao} atualizada em {Pasta}", edicao.Id, pasta);
    }

    private static async Task BaixarParaArquivoAsync(HttpClient cliente, AuthenticationHeaderValue credencial, string url, string destino, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(cliente, credencial, url, ct);
        await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
        await using var arquivo = File.Create(destino);
        await origem.CopyToAsync(arquivo, ct);
    }

    private static async Task ConferirHashAsync(HttpClient cliente, AuthenticationHeaderValue credencial, string url, string arquivo, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(cliente, credencial, url, ct);
        var esperado = (await resposta.Content.ReadAsStringAsync(ct)).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

        await using var fluxo = File.OpenRead(arquivo);
        var calculado = Convert.ToHexStringLower(await SHA256.HashDataAsync(fluxo, ct));
        if (!string.Equals(esperado, calculado, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA-256 divergente para {Path.GetFileName(arquivo)}");
    }

    /// <summary>Envia com autenticacao para a MaxMind e segue ate 3 redirecionamentos sem a credencial.</summary>
    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, AuthenticationHeaderValue credencial, string url, CancellationToken ct)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Authorization = credencial;
        var resposta = await cliente.SendAsync(requisicao, HttpCompletionOption.ResponseHeadersRead, ct);

        for (var saltos = 0; saltos < 3 && resposta.StatusCode is >= HttpStatusCode.MovedPermanently and <= HttpStatusCode.PermanentRedirect; saltos++)
        {
            var destino = resposta.Headers.Location ?? throw new HttpRequestException("Redirecionamento sem destino.");
            if (!destino.IsAbsoluteUri)
                destino = new Uri(new Uri(url), destino);
            if (destino.Scheme != Uri.UriSchemeHttps)
                throw new HttpRequestException("Redirecionamento para endereço não seguro recusado.");

            resposta.Dispose();
            resposta = await cliente.SendAsync(new HttpRequestMessage(HttpMethod.Get, destino), HttpCompletionOption.ResponseHeadersRead, ct);
        }

        if (!resposta.IsSuccessStatusCode)
        {
            var codigo = resposta.StatusCode;
            resposta.Dispose();
            throw new HttpRequestException($"MaxMind respondeu {(int)codigo}", null, codigo);
        }

        return resposta;
    }

    private static async Task<Dictionary<string, string>> ExtrairTarGzAsync(string pacote, IReadOnlyList<string> desejados, string pastaTemporaria, CancellationToken ct)
    {
        var extraidos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var arquivo = File.OpenRead(pacote);
        await using var descompactado = new GZipStream(arquivo, CompressionMode.Decompress);
        await using var leitor = new TarReader(descompactado);

        while (await leitor.GetNextEntryAsync(copyData: false, ct) is { } entrada)
        {
            var nome = Path.GetFileName(entrada.Name);
            if (entrada.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || !desejados.Contains(nome, StringComparer.OrdinalIgnoreCase))
                continue;

            var destino = Path.Combine(pastaTemporaria, nome);
            await entrada.ExtractToFileAsync(destino, overwrite: true, ct);
            extraidos[nome] = destino;
        }

        return extraidos;
    }

    private static Dictionary<string, string> ExtrairZip(string pacote, IReadOnlyList<string> desejados, string pastaTemporaria)
    {
        var extraidos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(pacote);
        foreach (var entrada in zip.Entries)
        {
            if (!desejados.Contains(entrada.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var destino = Path.Combine(pastaTemporaria, entrada.Name);
            entrada.ExtractToFile(destino, overwrite: true);
            extraidos[entrada.Name] = destino;
        }

        return extraidos;
    }

    private void TentarApagar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta))
                Directory.Delete(pasta, recursive: true);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Nao foi possivel limpar a pasta temporaria {Pasta}", pasta);
        }
    }

    private sealed record EdicaoGeo(string Id, string Sufixo, IReadOnlyList<string> Arquivos);
}
