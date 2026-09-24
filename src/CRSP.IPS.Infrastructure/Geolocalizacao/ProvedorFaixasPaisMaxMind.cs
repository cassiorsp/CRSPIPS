using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRSP.IPS.Infrastructure.Geolocalizacao;

/// <summary>
/// Le as faixas de IP de cada pais da base GeoLite2-Country em CSV e devolve as faixas ja mescladas
/// (redes contiguas viram um unico intervalo, reduzindo o numero de regras no firewall).
/// </summary>
internal sealed class ProvedorFaixasPaisMaxMind(IOptions<OpcoesCrspips> opcoes, ILogger<ProvedorFaixasPaisMaxMind> logger) : IProvedorFaixasPais
{
    private readonly string _pasta = opcoes.Value.PastaGeoIp;
    private readonly Lock _trava = new();
    private (string Chave, IReadOnlyList<FaixaIp> Faixas)? _cache;

    public bool EstaDisponivel =>
        LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoBlocosIPv4) is not null &&
        LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoLocais) is not null;

    public IReadOnlyList<FaixaIp> ObterFaixas(IReadOnlySet<string> paises)
    {
        var locais = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoLocais);
        var blocosIPv4 = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoBlocosIPv4);
        var blocosIPv6 = LocalizadorArquivosGeo.Encontrar(_pasta, LocalizadorArquivosGeo.ArquivoBlocosIPv6);
        if (locais is null || blocosIPv4 is null || paises.Count == 0)
            return [];

        var chave = $"{string.Join(',', paises.Order())}|{blocosIPv4.LastWriteTimeUtc:O}";
        lock (_trava)
        {
            if (_cache is { } cache && cache.Chave == chave)
                return cache.Faixas;

            var idsGeo = LerIdsDosPaises(locais.FullName, paises);
            var faixas = new List<FaixaIp>();
            LerBlocos(blocosIPv4.FullName, idsGeo, faixas);
            if (blocosIPv6 is not null)
                LerBlocos(blocosIPv6.FullName, idsGeo, faixas);

            var mescladas = FaixaIp.Mesclar(faixas);
            logger.LogInformation(
                "Faixas de paises {Paises}: {Redes} redes mescladas em {Faixas} faixas",
                string.Join(',', paises), faixas.Count, mescladas.Count);

            _cache = (chave, mescladas);
            return mescladas;
        }
    }

    /// <summary>Colunas: geoname_id,locale_code,continent_code,continent_name,country_iso_code,...</summary>
    private static HashSet<string> LerIdsDosPaises(string caminho, IReadOnlySet<string> paises)
    {
        var ids = new HashSet<string>();
        foreach (var linha in File.ReadLines(caminho).Skip(1))
        {
            var colunas = linha.Split(',');
            if (colunas.Length > 4 && paises.Contains(colunas[4].Trim('"')))
                ids.Add(colunas[0]);
        }

        return ids;
    }

    /// <summary>Colunas: network,geoname_id,registered_country_geoname_id,...</summary>
    private static void LerBlocos(string caminho, HashSet<string> idsGeo, List<FaixaIp> destino)
    {
        foreach (var linha in File.ReadLines(caminho).Skip(1))
        {
            var colunas = linha.Split(',', 4);
            if (colunas.Length < 3)
                continue;

            var id = string.IsNullOrEmpty(colunas[1]) ? colunas[2] : colunas[1];
            if (idsGeo.Contains(id) && FaixaIp.TentarConverter(colunas[0], out var faixa) && faixa is not null)
                destino.Add(faixa);
        }
    }
}
