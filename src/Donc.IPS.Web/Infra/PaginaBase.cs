using System.Security.Claims;
using Donc.IPS.Application.Modelos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Donc.IPS.Web.Infra;

/// <summary>Base das paginas: mensagens de retorno (TempData) e redirecionamento seguro apos POST.</summary>
public abstract class PaginaBase : PageModel
{
    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    protected int IdUsuarioLogado => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected IActionResult Concluir(Resultado resultado, string mensagemSucesso, string? retorno = null)
    {
        if (resultado.Sucesso)
            MensagemSucesso = mensagemSucesso;
        else
            MensagemErro = resultado.Erro;

        return !string.IsNullOrEmpty(retorno) && Url.IsLocalUrl(retorno) ? LocalRedirect(retorno) : RedirectToPage();
    }
}
