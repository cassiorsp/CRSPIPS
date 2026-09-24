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

        var bloqueados = firewall.LerEnderecos(ConjuntoRegrasFirewall.Bloqueios);
        var externas = firewall.LerEnderecos(ConjuntoRegrasFirewall.ListasExternas);
        var permissivas = firewall.ListarRegrasPermissivasNasPortas([3389]);

        Assert.NotNull(bloqueados);
        Assert.NotNull(externas);
        Assert.NotNull(permissivas);
    }
}
