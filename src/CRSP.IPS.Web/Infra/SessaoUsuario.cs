using System.Security.Claims;
using CRSP.IPS.Domain.Entidades;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CRSP.IPS.Web.Infra;

/// <summary>Cookie de autenticacao do painel.</summary>
public static class SessaoUsuario
{
    public static Task EntrarAsync(HttpContext contexto, Usuario usuario)
    {
        var identidade = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
    }

    /// <summary>Somente caminhos locais ("/..."), para o login nao virar um redirecionador aberto.</summary>
    public static string RetornoSeguro(string? retorno) =>
        !string.IsNullOrEmpty(retorno) && retorno.StartsWith('/') && !retorno.StartsWith("//") && !retorno.StartsWith("/\\") ? retorno : "/";
}
