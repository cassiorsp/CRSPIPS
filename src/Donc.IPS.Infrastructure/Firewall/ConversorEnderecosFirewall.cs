using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Infrastructure.Firewall;

/// <summary>Converte os formatos devolvidos pelo Windows Firewall ("1.2.3.4/255.255.255.255", "a-b", "*", palavras-chave).</summary>
internal static class ConversorEnderecosFirewall
{
    public static IEnumerable<FaixaIp> Converter(string? enderecosRemotos)
    {
        if (string.IsNullOrWhiteSpace(enderecosRemotos) || enderecosRemotos == "*")
            yield break;

        foreach (var parte in enderecosRemotos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (FaixaIp.TentarConverter(parte, out var faixa) && faixa is not null)
                yield return faixa;
        }
    }

    /// <summary>Verifica se a lista de portas da regra ("80,443", "5000-5010", "*") contem alguma das portas informadas.</summary>
    public static bool PortasContemAlguma(string? portasRegra, IReadOnlyList<int> portas)
    {
        if (string.IsNullOrWhiteSpace(portasRegra) || portasRegra == "*")
            return false;

        foreach (var parte in portasRegra.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var hifen = parte.IndexOf('-');
            if (hifen > 0 &&
                int.TryParse(parte[..hifen], out var inicio) &&
                int.TryParse(parte[(hifen + 1)..], out var fim) &&
                portas.Any(p => p >= inicio && p <= fim))
                return true;

            if (int.TryParse(parte, out var porta) && portas.Contains(porta))
                return true;
        }

        return false;
    }
}
