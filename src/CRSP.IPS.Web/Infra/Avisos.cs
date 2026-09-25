using CRSP.IPS.Application.Modelos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// Mensagem de retorno das acoes (sucesso ou erro), exibida no topo da pagina. Os textos sao chaves pt-BR.
/// Uma mensagem "apos navegar" sobrevive a proxima troca de pagina (ex.: salvar e voltar para a lista).
/// </summary>
public sealed class Avisos : IDisposable
{
    private readonly NavigationManager _navegacao;
    private Aviso? _pendente;

    public Avisos(NavigationManager navegacao)
    {
        _navegacao = navegacao;
        _navegacao.LocationChanged += AoNavegar;
    }

    public Aviso? Atual { get; private set; }

    public event Action? Mudou;

    public void Sucesso(string texto) => Definir(new Aviso(true, texto));

    public void Erro(string texto) => Definir(new Aviso(false, texto));

    /// <summary>Mostra o erro do resultado ou a mensagem de sucesso. Retorna se deu certo.</summary>
    public bool Mostrar(Resultado resultado, string mensagemSucesso)
    {
        if (resultado.Sucesso)
            Sucesso(mensagemSucesso);
        else
            Erro(resultado.Erro ?? "Ocorreu um erro ao processar a solicitação. Os detalhes foram registrados no log.");
        return resultado.Sucesso;
    }

    public void SucessoAposNavegar(string texto) => _pendente = new Aviso(true, texto);

    public void Fechar() => Definir(null);

    private void Definir(Aviso? aviso)
    {
        Atual = aviso;
        Mudou?.Invoke();
    }

    private void AoNavegar(object? remetente, LocationChangedEventArgs args)
    {
        var proximo = _pendente;
        _pendente = null;
        Definir(proximo);
    }

    public void Dispose() => _navegacao.LocationChanged -= AoNavegar;
}

public sealed record Aviso(bool Sucesso, string Texto);
