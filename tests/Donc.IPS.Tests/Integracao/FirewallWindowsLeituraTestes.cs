using Donc.IPS.Infrastructure.Firewall;
using Microsoft.Extensions.Logging.Abstractions;

namespace Donc.IPS.Tests.Integracao;

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

        var bloqueados = firewall.LerEnderecosBloqueados();
        var permissivas = firewall.ListarRegrasPermissivasNasPortas([3389]);

        Assert.NotNull(bloqueados);
        Assert.NotNull(permissivas);
    }
}
