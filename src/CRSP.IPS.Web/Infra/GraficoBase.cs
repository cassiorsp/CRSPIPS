using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// Base dos graficos Chart.js (wwwroot/js/graficos.js). O desenho so acontece no navegador, depois da renderizacao
/// interativa; o grafico so e redesenhado quando os dados mudam (a pagina pode renderizar por outros motivos).
/// </summary>
public abstract class GraficoBase : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

    protected ElementReference Canvas;

    private object? _dadosDesenhados;

    /// <summary>Funcao de crspips.graficos que desenha este tipo de grafico.</summary>
    protected abstract string Funcao { get; }

    /// <summary>Dados enviados ao JavaScript (listas simples, serializadas em JSON).</summary>
    protected abstract object MontarDados();

    /// <summary>Referencia que muda quando os dados mudam (a lista recebida por parametro).</summary>
    protected abstract object Versao { get; }

    protected override async Task OnAfterRenderAsync(bool primeiraRenderizacao)
    {
        if (ReferenceEquals(_dadosDesenhados, Versao))
            return;

        _dadosDesenhados = Versao;
        await JS.InvokeVoidAsync($"crspips.graficos.{Funcao}", Canvas, MontarDados());
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        // Na pre-renderizacao nada foi desenhado e o JavaScript nem esta disponivel (a chamada lancaria excecao).
        if (_dadosDesenhados is null)
            return;

        try
        {
            await JS.InvokeVoidAsync("crspips.graficos.destruir", Canvas);
        }
        catch (JSDisconnectedException)
        {
            // Circuito ja encerrado (aba fechada): o grafico some junto com a pagina.
        }
    }
}
