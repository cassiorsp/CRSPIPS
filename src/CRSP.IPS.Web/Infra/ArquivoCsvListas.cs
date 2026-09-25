using System.Text;
using CRSP.IPS.Application.Modelos;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// CSV de importacao da lista branca/negra: colunas "faixa" e "descricao" (opcional). Aceita ponto e virgula
/// (padrao do Excel em pt-BR) ou virgula como separador, campos entre aspas e linhas de comentario iniciadas por #.
/// </summary>
public static class ArquivoCsvListas
{
    public const long TamanhoMaximoBytes = 1024 * 1024;

    public static string GerarModelo() =>
        "faixa;descricao\r\n" +
        "203.0.113.7;IP único\r\n" +
        "198.51.100.0/24;Rede em CIDR\r\n" +
        "192.0.2.10-192.0.2.50;Intervalo de endereços\r\n" +
        "2001:db8::/32;Faixa IPv6\r\n";

    public static async Task<IReadOnlyList<LinhaImportacao>> LerAsync(Stream fluxo, CancellationToken ct = default)
    {
        using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var linhas = new List<LinhaImportacao>();
        char? separador = null;
        var numero = 0;

        while (await leitor.ReadLineAsync(ct) is { } linha)
        {
            numero++;
            if (string.IsNullOrWhiteSpace(linha) || linha.TrimStart().StartsWith('#'))
                continue;

            separador ??= linha.Contains(';') ? ';' : ',';
            var campos = Separar(linha, separador.Value);
            var faixa = campos.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
            if (faixa.Length == 0 || EhCabecalho(faixa))
                continue;

            var descricao = campos.ElementAtOrDefault(1)?.Trim();
            linhas.Add(new LinhaImportacao(numero, faixa, string.IsNullOrEmpty(descricao) ? null : descricao));
        }

        return linhas;
    }

    private static bool EhCabecalho(string campo) =>
        campo.Equals("faixa", StringComparison.OrdinalIgnoreCase) || campo.Equals("ip", StringComparison.OrdinalIgnoreCase);

    private static List<string> Separar(string linha, char separador)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (c == '"')
            {
                if (entreAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    entreAspas = !entreAspas;
                }
            }
            else if (c == separador && !entreAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos;
    }
}
