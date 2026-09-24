namespace Donc.IPS.Infrastructure.Geolocalizacao;

/// <summary>
/// Encontra os arquivos MaxMind dentro da pasta configurada (busca recursiva, pois o zip da MaxMind
/// extrai em subpastas datadas). Quando ha mais de um, usa o mais recente.
/// </summary>
internal static class LocalizadorArquivosGeo
{
    public const string ArquivoCidade = "GeoLite2-City.mmdb";
    public const string ArquivoAsn = "GeoLite2-ASN.mmdb";
    public const string ArquivoBlocosIPv4 = "GeoLite2-Country-Blocks-IPv4.csv";
    public const string ArquivoBlocosIPv6 = "GeoLite2-Country-Blocks-IPv6.csv";
    public const string ArquivoLocais = "GeoLite2-Country-Locations-en.csv";
    public const string PastaTemporaria = ".download";

    public static FileInfo? Encontrar(string pasta, string nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta))
            return null;

        try
        {
            return new DirectoryInfo(pasta)
                .EnumerateFiles(nomeArquivo, SearchOption.AllDirectories)
                .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}{PastaTemporaria}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
