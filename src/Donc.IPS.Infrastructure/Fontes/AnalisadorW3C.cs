using System.Globalization;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Infrastructure.Fontes;

/// <summary>
/// Interpreta linhas no formato W3C Extended (usado pelo IIS e pelo HTTPERR). As colunas variam conforme
/// a configuracao de log, por isso o mapeamento vem da diretiva "#Fields:" do proprio arquivo.
/// </summary>
internal sealed class AnalisadorW3C
{
    private readonly Dictionary<string, int> _colunas;

    private AnalisadorW3C(Dictionary<string, int> colunas) => _colunas = colunas;

    public static bool EhDiretiva(string linha) => linha.StartsWith('#');

    public static AnalisadorW3C? DeDiretivaCampos(string linha)
    {
        const string prefixo = "#Fields:";
        if (!linha.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            return null;

        var campos = linha[prefixo.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var colunas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < campos.Length; i++)
            colunas.TryAdd(campos[i], i);

        return colunas.ContainsKey("c-ip") && colunas.ContainsKey("date") && colunas.ContainsKey("time")
            ? new AnalisadorW3C(colunas)
            : null;
    }

    /// <param name="idSiteDoArquivo">ID do site deduzido da pasta do log (W3SVC14 → "14").</param>
    /// <param name="nomeDoSite">Traduz o ID do site para o nome configurado no IIS.</param>
    public EventoDetectado? Interpretar(string linha, TipoFonte fonte, string? idSiteDoArquivo = null, Func<string, string?>? nomeDoSite = null)
    {
        var valores = linha.Split(' ');
        if (!EnderecoIp.TentarConverter(Valor(valores, "c-ip"), out var ip))
            return null;

        if (!DateTime.TryParseExact($"{Valor(valores, "date")} {Valor(valores, "time")}", "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var momento))
            return null;

        var url = Valor(valores, "cs-uri-stem") ?? Valor(valores, "cs-uri");
        var consulta = Valor(valores, "cs-uri-query");
        if (url is not null && consulta is not null)
            url = $"{url}?{consulta}";

        return new EventoDetectado(
            ip,
            fonte,
            DateTime.SpecifyKind(momento, DateTimeKind.Utc),
            Metodo: Valor(valores, "cs-method"),
            Url: url,
            CodigoStatus: int.TryParse(Valor(valores, "sc-status"), out var status) ? status : null,
            UserAgent: Valor(valores, "cs(User-Agent)")?.Replace('+', ' '),
            MotivoHttpErr: Valor(valores, "s-reason"),
            Site: ObterSite(valores, idSiteDoArquivo, nomeDoSite));
    }

    /// <summary>
    /// Prioridade: cs-host (hostname da requisicao, se habilitado no log) > nome do site pelo ID
    /// (s-siteid do HTTPERR, s-sitename "W3SVC14" ou a pasta do log).
    /// </summary>
    private string? ObterSite(string[] valores, string? idSiteDoArquivo, Func<string, string?>? nomeDoSite)
    {
        var host = Valor(valores, "cs-host");
        if (host is not null)
            return host;

        var id = Valor(valores, "s-siteid") ?? ExtrairIdSite(Valor(valores, "s-sitename")) ?? idSiteDoArquivo;
        if (id is null)
            return null;

        return nomeDoSite?.Invoke(id) ?? $"Site {id}";
    }

    public static string? ExtrairIdSite(string? nomePastaOuSite) =>
        nomePastaOuSite is not null &&
        nomePastaOuSite.StartsWith("W3SVC", StringComparison.OrdinalIgnoreCase) &&
        nomePastaOuSite.Length > 5 &&
        nomePastaOuSite[5..].All(char.IsAsciiDigit)
            ? nomePastaOuSite[5..]
            : null;

    /// <summary>No W3C, "-" representa valor vazio.</summary>
    private string? Valor(string[] valores, string campo)
    {
        if (!_colunas.TryGetValue(campo, out var indice) || indice >= valores.Length)
            return null;
        var valor = valores[indice];
        return valor is "-" or "" ? null : valor;
    }
}
