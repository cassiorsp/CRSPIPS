using System.Globalization;

namespace CRSP.IPS.Domain.ObjetosValor;

/// <summary>Converte duracoes amigaveis ("30m", "1h", "7d") de/para TimeSpan.</summary>
public static class DuracaoTexto
{
    public static bool TentarConverter(string? texto, out TimeSpan duracao)
    {
        duracao = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        texto = texto.Trim().ToLowerInvariant();
        if (texto.Length < 2)
            return false;

        if (!int.TryParse(texto[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantidade) || quantidade <= 0)
            return false;

        duracao = texto[^1] switch
        {
            'm' => TimeSpan.FromMinutes(quantidade),
            'h' => TimeSpan.FromHours(quantidade),
            'd' => TimeSpan.FromDays(quantidade),
            _ => TimeSpan.Zero
        };
        return duracao > TimeSpan.Zero;
    }

    public static bool TentarConverterLista(string? texto, out IReadOnlyList<TimeSpan> duracoes)
    {
        var lista = new List<TimeSpan>();
        duracoes = lista;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        foreach (var parte in texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TentarConverter(parte, out var duracao))
                return false;
            lista.Add(duracao);
        }

        return lista.Count > 0;
    }

    public static string Formatar(TimeSpan duracao)
    {
        if (duracao.TotalDays >= 1 && duracao.TotalDays % 1 == 0)
            return $"{(int)duracao.TotalDays}d";
        if (duracao.TotalHours >= 1 && duracao.TotalHours % 1 == 0)
            return $"{(int)duracao.TotalHours}h";
        return $"{(int)Math.Ceiling(duracao.TotalMinutes)}m";
    }

    public static string FormatarLista(IEnumerable<TimeSpan> duracoes) => string.Join(", ", duracoes.Select(Formatar));
}
