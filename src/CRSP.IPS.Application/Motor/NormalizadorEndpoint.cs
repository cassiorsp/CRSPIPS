namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Reduz a URL de uma requisicao ao "endpoint": sem query string, em minusculas, com identificadores trocados por
/// {id} (/api/os/123 e /api/os/456 viram /api/os/{id}) e arquivos estaticos agrupados.
/// </summary>
public static class NormalizadorEndpoint
{
    public const string Estaticos = "(arquivos estáticos)";
    public const string Outros = "(outros)";

    private const int MaximoSegmentos = 8;
    private const int MaximoCaracteres = 300;

    private static readonly HashSet<string> ExtensoesEstaticas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp", ".bmp",
        ".woff", ".woff2", ".ttf", ".eot", ".otf", ".mp4", ".webm", ".pdf"
    };

    public static string Normalizar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "/";

        var caminho = url;
        var corte = caminho.IndexOf('?');
        if (corte >= 0)
            caminho = caminho[..corte];

        var extensao = Path.GetExtension(caminho);
        if (extensao.Length > 0 && ExtensoesEstaticas.Contains(extensao))
            return Estaticos;

        var segmentos = caminho.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segmentos.Length == 0)
            return "/";

        var normalizado = string.Concat(segmentos
            .Take(MaximoSegmentos)
            .Select(s => "/" + (EhIdentificador(s) ? "{id}" : s.ToLowerInvariant())));

        return normalizado.Length > MaximoCaracteres ? normalizado[..MaximoCaracteres] : normalizado;
    }

    private static bool EhIdentificador(string segmento)
    {
        if (segmento.All(char.IsAsciiDigit))
            return true;
        if (Guid.TryParse(segmento, out _))
            return true;
        // Hash ou token: longo e hexadecimal, ou longo e misturando letras e digitos.
        if (segmento.Length >= 16 && segmento.All(char.IsAsciiHexDigit))
            return true;
        return segmento.Length >= 24 && segmento.Any(char.IsAsciiDigit) && segmento.Any(char.IsAsciiLetter);
    }
}
