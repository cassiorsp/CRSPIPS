using System.Text.Json;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Motor;

public sealed record EntradaInterpretada(FaixaIp Faixa, string? Referencia);

/// <summary>Converte o texto baixado de uma lista externa em faixas de IP. Linhas invalidas sao ignoradas.</summary>
public static class InterpretadorListaExterna
{
    public static IReadOnlyList<EntradaInterpretada> Interpretar(string conteudo, FormatoListaExterna formato)
    {
        var entradas = new List<EntradaInterpretada>();
        foreach (var bruta in conteudo.Split('\n'))
        {
            var linha = bruta.Trim();
            if (linha.Length == 0 || linha[0] is '#' or ';')
                continue;

            var entrada = formato switch
            {
                FormatoListaExterna.SpamhausJson => InterpretarSpamhaus(linha),
                FormatoListaExterna.DShield => InterpretarDShield(linha),
                _ => InterpretarTexto(linha)
            };

            if (entrada is not null)
                entradas.Add(entrada);
        }

        return entradas;
    }

    /// <summary>{"cidr":"1.10.16.0/20","sblid":"SBL256894","rir":"apnic"}; a linha de metadados nao tem "cidr".</summary>
    private static EntradaInterpretada? InterpretarSpamhaus(string linha)
    {
        try
        {
            using var json = JsonDocument.Parse(linha);
            if (!json.RootElement.TryGetProperty("cidr", out var cidr) ||
                !FaixaIp.TentarConverter(cidr.GetString(), out var faixa) || faixa is null)
                return null;

            var referencia = json.RootElement.TryGetProperty("sblid", out var sbl) ? sbl.GetString() : null;
            return new EntradaInterpretada(faixa, referencia);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Colunas: Start, End, Netblock, Attacks, Name, Country, email (a linha de cabecalho e ignorada).</summary>
    private static EntradaInterpretada? InterpretarDShield(string linha)
    {
        var colunas = linha.Split('\t', StringSplitOptions.TrimEntries);
        if (colunas.Length < 2 ||
            !EnderecoIp.TentarConverter(colunas[0], out var inicio) ||
            !EnderecoIp.TentarConverter(colunas[1], out var fim) ||
            inicio.EhIPv4 != fim.EhIPv4)
            return null;

        return new EntradaInterpretada(FaixaIp.DeIntervalo(inicio, fim), colunas.Length > 4 ? colunas[4] : null);
    }

    /// <summary>Primeiro campo da linha (separado por espaco, tabulacao, virgula ou ";"). Ex.: IPsum "1.2.3.4\t7".</summary>
    private static EntradaInterpretada? InterpretarTexto(string linha)
    {
        var primeiro = linha.Split([' ', '\t', ',', ';'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return FaixaIp.TentarConverter(primeiro, out var faixa) && faixa is not null ? new EntradaInterpretada(faixa, null) : null;
    }
}
