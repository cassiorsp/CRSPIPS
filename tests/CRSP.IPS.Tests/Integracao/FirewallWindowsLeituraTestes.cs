using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Infrastructure.Firewall;
using Microsoft.Extensions.Logging.Abstractions;

namespace CRSP.IPS.Tests.Integracao;

/// <summary>
/// Somente leitura do Windows Firewall real (nao requer administrador e nao altera nada).
/// Valida que a enumeracao COM via dynamic funciona nesta versao do .NET/Windows.
/// </summary>
[Trait("Categoria", "WindowsFirewall")]
public class FirewallWindowsLeituraTestes
{
    [Fact]
    public void LeRegrasSemAlterarOFirewall()
    {
        var firewall = new FirewallWindows(NullLogger<FirewallWindows>.Instance);

        foreach (var conjunto in Enum.GetValues<ConjuntoRegrasFirewall>())
            Assert.NotNull(firewall.LerEnderecos(conjunto));
        Assert.NotNull(firewall.ListarRegrasPermissivasNasPortas([3389]));
    }

    [Fact]
    public void NomesDasRegrasSeguemAFonte()
    {
        Assert.Equal("CRSPIPS_LOGIIS_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.LogIis).Prefixo);
        Assert.Equal("CRSPIPS_HTTPERR_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.HttpErr).Prefixo);
        Assert.Equal("CRSPIPS_EVENTOWINDOWS_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.EventoWindows).Prefixo);
        Assert.Equal("CRSPIPS_MANUAL_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.Manual).Prefixo);
        Assert.Equal("CRSPIPS_LISTANEGRA_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.ListaNegra).Prefixo);
        Assert.Equal("CRSPIPS_LISTAEXTERNA_", FirewallWindows.Identificar(ConjuntoRegrasFirewall.ListasExternas).Prefixo);
    }
}
