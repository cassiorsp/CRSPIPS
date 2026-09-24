using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;

namespace CRSP.IPS.Application.Motor;

/// <summary>Verifica se um evento casa com o criterio de uma regra. Expressoes regulares ficam em cache.</summary>
public sealed class AvaliadorRegras
{
    private static readonly TimeSpan TempoMaximoRegex = TimeSpan.FromMilliseconds(250);
    private readonly ConcurrentDictionary<string, Regex> _expressoes = new();

    public bool Corresponde(RegraDeteccao regra, EventoDetectado evento)
    {
        if (!regra.Ativa || regra.Fonte != evento.Fonte)
            return false;

        return regra.Criterio switch
        {
            TipoCriterio.CodigoStatus => evento.CodigoStatus is { } codigo && ContemNumero(regra.Padrao, codigo),
            TipoCriterio.IdEventoWindows => evento.IdEventoWindows is { } id && ContemNumero(regra.Padrao, id),
            TipoCriterio.PadraoUrl => CasaExpressao(regra.Padrao, evento.Url),
            TipoCriterio.MotivoHttpErr => CasaExpressao(regra.Padrao, evento.MotivoHttpErr),
            _ => false
        };
    }

    private static bool ContemNumero(string lista, int valor)
    {
        foreach (var parte in lista.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(parte, out var numero) && numero == valor)
                return true;
        }

        return false;
    }

    private bool CasaExpressao(string padrao, string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return false;

        var regex = _expressoes.GetOrAdd(padrao, Criar);
        try
        {
            return regex.IsMatch(valor);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Motor sem retrocesso: tempo linear no tamanho da URL, imune a ReDoS e sem risco de "timeout" deixar um
    /// ataque passar sob carga. Padroes com recursos que ele nao suporta (lookaround, backreference) usam o
    /// motor tradicional com limite de tempo.
    /// </summary>
    private static Regex Criar(string padrao)
    {
        const RegexOptions opcoes = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        try
        {
            return new Regex(padrao, opcoes | RegexOptions.NonBacktracking);
        }
        catch (NotSupportedException)
        {
            return new Regex(padrao, opcoes | RegexOptions.Compiled, TempoMaximoRegex);
        }
    }
}
