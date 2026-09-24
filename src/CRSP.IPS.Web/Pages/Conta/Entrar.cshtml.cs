using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CRSP.IPS.Web.Pages.Conta;

[EnableRateLimiting(PoliticasLimite.Login)]
public sealed class EntrarModel(ServicoUsuarios servicoUsuarios, IContextoUsuario contextoIp) : PaginaBase
{
    [BindProperty]
    public DadosEntrada Entrada { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? RetornoUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await servicoUsuarios.ExisteAlgumAsync())
            return RedirectToPage("/Conta/PrimeiroAcesso");
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var resultado = await servicoUsuarios.AutenticarAsync(Entrada.Email, Entrada.Senha, contextoIp.Ip);
        if (!resultado.Sucesso || resultado.Valor is null)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro ?? "E-mail ou senha inválidos.");
            return Page();
        }

        await AutenticarAsync(HttpContext, resultado.Valor);
        return !string.IsNullOrEmpty(RetornoUrl) && Url.IsLocalUrl(RetornoUrl) ? LocalRedirect(RetornoUrl) : RedirectToPage("/Index");
    }

    internal static Task AutenticarAsync(HttpContext contexto, Usuario usuario)
    {
        var identidade = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));
    }

    public sealed class DadosEntrada
    {
        [Required(ErrorMessage = "Campo obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;
    }
}
