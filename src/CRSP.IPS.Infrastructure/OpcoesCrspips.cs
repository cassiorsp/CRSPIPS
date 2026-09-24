namespace CRSP.IPS.Infrastructure;

/// <summary>Secao "CRSPIPS" do appsettings.json (Web e Worker devem apontar para o mesmo banco).</summary>
public sealed class OpcoesCrspips
{
    public const string Secao = "CRSPIPS";

    public string CaminhoBanco { get; set; } = @"C:\ProgramData\CRSPIPS\crspips.db";

    /// <summary>
    /// Pasta com as bases MaxMind (busca recursiva): GeoLite2-City.mmdb, GeoLite2-ASN.mmdb e
    /// GeoLite2-Country-Blocks-IPv4.csv / -IPv6.csv / -Locations-en.csv.
    /// </summary>
    public string PastaGeoIp { get; set; } = @"C:\ProgramData\CRSPIPS\GeoIP";
}
