using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Motor;

/// <summary>
/// Copia em memoria das faixas das listas externas em uso (modos Avaliacao e Ativa), para verificar rapidamente
/// se um IP do trafego real pertence a alguma lista (busca binaria por lista). Recarrega quando as listas mudam.
/// </summary>
public sealed class MapaListasExternas
{
    private readonly Lock _trava = new();
    private string? _versao;
    private IReadOnlyList<(int ListaId, string[] Inicios, string[] Fins)> _listas = [];

    public async Task AtualizarSeNecessarioAsync(IRepositorioListasExternas repositorio, CancellationToken ct)
    {
        var versao = await repositorio.ObterVersaoAsync(ct);
        if (versao == _versao)
            return;

        var listas = await repositorio.ListarAsync(ct);
        var entradas = await repositorio.ListarEntradasAsync(listas.Where(l => l.EstaEmUso).Select(l => l.Id).ToList(), ct);
        var novas = entradas
            .GroupBy(e => e.ListaExternaId)
            .Select(g =>
            {
                var faixas = FaixaIp.Mesclar(g.Select(e => e.ObterFaixa()))
                    .OrderBy(f => f.Inicio.ObterChaveOrdenavel(), StringComparer.Ordinal)
                    .ToList();
                return (g.Key, faixas.Select(f => f.Inicio.ObterChaveOrdenavel()).ToArray(), faixas.Select(f => f.Fim.ObterChaveOrdenavel()).ToArray());
            })
            .ToList();

        lock (_trava)
        {
            _listas = novas;
            _versao = versao;
        }
    }

    public IReadOnlyList<int> ListasQueContem(EnderecoIp ip)
    {
        var chave = ip.ObterChaveOrdenavel();
        var listas = _listas;
        var resultado = new List<int>();
        foreach (var (listaId, inicios, fins) in listas)
        {
            var posicao = Array.BinarySearch(inicios, chave, StringComparer.Ordinal);
            if (posicao < 0)
                posicao = ~posicao - 1;
            if (posicao >= 0 && string.CompareOrdinal(chave, fins[posicao]) <= 0)
                resultado.Add(listaId);
        }

        return resultado;
    }
}
