using CRSP.IPS.Application.Modelos;

namespace CRSP.IPS.Web.Infra;

/// <summary>Filtro e ordenacao das tabelas de Endpoints e IIS, usados pela tela e pela exportacao CSV.</summary>
public static class OrdenacaoMetricas
{
    public const string EndpointsPadrao = "total";
    public const string SitesPadrao = "acessos";
    public const string PoolsPadrao = "memoria";

    public static IEnumerable<LinhaRequisicoes> Filtrar(IEnumerable<LinhaRequisicoes> linhas, string? busca, bool somenteErros)
    {
        if (somenteErros)
            linhas = linhas.Where(l => l.ErroServidor > 0 || l.ErroCliente > 0);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var texto = busca.Trim();
            linhas = linhas.Where(l => l.Endpoint.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        return linhas;
    }

    public static IEnumerable<LinhaRequisicoes> Ordenar(IEnumerable<LinhaRequisicoes> linhas, string? coluna, bool decrescente) =>
        Ordenar(linhas, decrescente, coluna switch
        {
            "semerro" => l => l.Sucesso + l.Redirecionamento,
            "4xx" => l => l.ErroCliente,
            "5xx" => l => l.ErroServidor,
            "erro" => l => l.PercentualErro,
            "media" => l => l.TempoMedioMs,
            "max" => l => l.TempoMaximoMs,
            _ => l => l.Total
        });

    public static IEnumerable<ResumoSiteIis> Ordenar(IEnumerable<ResumoSiteIis> sites, string? coluna, bool decrescente) =>
        Ordenar(sites, decrescente, coluna switch
        {
            "5xx" => s => s.Requisicoes.ErroServidor,
            "tempo" => s => s.Requisicoes.TempoMedioMs,
            "memmin" => s => s.Consumo?.MemoriaMinima,
            "memmedia" => s => s.Consumo?.MemoriaMedia,
            "memmax" => s => s.Consumo?.MemoriaMaxima,
            "cpu" => s => s.Consumo?.CpuMedia,
            _ => s => s.Requisicoes.Total
        });

    public static IEnumerable<ResumoPool> Ordenar(IEnumerable<ResumoPool> pools, string? coluna, bool decrescente) =>
        Ordenar(pools, decrescente, coluna switch
        {
            "cpu" => p => p.CpuMaxima,
            "processos" => p => p.ProcessosMaximo,
            _ => p => p.MemoriaMaxima
        });

    /// <summary>Linhas sem valor (site sem amostra do pool) ficam sempre no fim.</summary>
    private static IEnumerable<T> Ordenar<T>(IEnumerable<T> itens, bool decrescente, Func<T, double?> chave)
    {
        var comValor = itens.OrderBy(i => chave(i) is null);
        return decrescente ? comValor.ThenByDescending(chave) : comValor.ThenBy(chave);
    }
}
