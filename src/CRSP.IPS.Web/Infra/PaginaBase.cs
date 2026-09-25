using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace CRSP.IPS.Web.Infra;

/// <summary>Base das paginas: servicos com escopo proprio, mensagens de retorno, traducao e confirmacao.</summary>
public abstract class PaginaBase : ComponentBase
{
    [Inject] protected ExecutorServicos Servicos { get; set; } = null!;
    [Inject] protected Avisos Avisos { get; set; } = null!;
    [Inject] protected NavigationManager Navegacao { get; set; } = null!;
    [Inject] protected IStringLocalizer<Textos> T { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [CascadingParameter] private Task<AuthenticationState>? EstadoAutenticacao { get; set; }

    protected async Task<int> ObterIdUsuarioAsync()
    {
        if (EstadoAutenticacao is null)
            return 0;
        var usuario = (await EstadoAutenticacao).User;
        return int.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    }

    /// <summary>Confirmacao nativa do navegador para acoes destrutivas.</summary>
    protected ValueTask<bool> ConfirmarAsync(string texto) => JS.InvokeAsync<bool>("confirm", texto);
}
