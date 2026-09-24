using System.Net;
using System.Numerics;

namespace Donc.IPS.Domain.ObjetosValor;

/// <summary>
/// Faixa continua de enderecos (inicio..fim inclusivo). Aceita IP unico, CIDR (10.0.0.0/8 ou 10.0.0.0/255.0.0.0)
/// ou intervalo (10.0.0.1-10.0.0.50).
/// </summary>
public sealed record FaixaIp
{
    public EnderecoIp Inicio { get; }
    public EnderecoIp Fim { get; }

    private FaixaIp(EnderecoIp inicio, EnderecoIp fim)
    {
        if (inicio.EhIPv4 != fim.EhIPv4)
            throw new ArgumentException("Inicio e fim da faixa devem ser da mesma familia (IPv4/IPv6).");
        if (inicio.CompareTo(fim) > 0)
            (inicio, fim) = (fim, inicio);
        Inicio = inicio;
        Fim = fim;
    }

    public bool EhEnderecoUnico => Inicio.Equals(Fim);

    public static FaixaIp DeEndereco(EnderecoIp endereco) => new(endereco, endereco);

    public static FaixaIp DeIntervalo(EnderecoIp inicio, EnderecoIp fim) => new(inicio, fim);

    public static bool TentarConverter(string? texto, out FaixaIp? faixa)
    {
        faixa = null;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        texto = texto.Trim();

        var hifen = texto.IndexOf('-');
        if (hifen > 0)
        {
            if (!EnderecoIp.TentarConverter(texto[..hifen], out var inicio) ||
                !EnderecoIp.TentarConverter(texto[(hifen + 1)..], out var fim) ||
                inicio.EhIPv4 != fim.EhIPv4)
                return false;
            faixa = new FaixaIp(inicio, fim);
            return true;
        }

        var barra = texto.IndexOf('/');
        if (barra < 0)
        {
            if (!EnderecoIp.TentarConverter(texto, out var unico))
                return false;
            faixa = DeEndereco(unico);
            return true;
        }

        if (!EnderecoIp.TentarConverter(texto[..barra], out var rede))
            return false;

        var sufixo = texto[(barra + 1)..];
        var bitsFamilia = rede.EhIPv4 ? 32 : 128;
        int prefixo;
        if (int.TryParse(sufixo, out var numero))
            prefixo = numero;
        else if (IPAddress.TryParse(sufixo, out var mascara) && TentarMascaraParaPrefixo(mascara, out var deMascara))
            prefixo = deMascara;
        else
            return false;

        if (prefixo < 0 || prefixo > bitsFamilia)
            return false;

        faixa = DeCidr(rede, prefixo);
        return true;
    }

    public static FaixaIp Converter(string texto) =>
        TentarConverter(texto, out var faixa)
            ? faixa!
            : throw new FormatException($"Faixa de IP invalida: '{texto}'.");

    public static FaixaIp DeCidr(EnderecoIp rede, int prefixo)
    {
        var bitsFamilia = rede.EhIPv4 ? 32 : 128;
        var bitsHost = bitsFamilia - prefixo;
        var mascaraHost = bitsHost == 0 ? BigInteger.Zero : (BigInteger.One << bitsHost) - 1;
        var inicio = ParaNumero(rede) & ~mascaraHost & MascaraFamilia(rede.EhIPv4);
        var fim = inicio | mascaraHost;
        return new FaixaIp(DeNumero(inicio, rede.EhIPv4), DeNumero(fim, rede.EhIPv4));
    }

    public bool Contem(EnderecoIp endereco) =>
        endereco.EhIPv4 == Inicio.EhIPv4 &&
        endereco.CompareTo(Inicio) >= 0 &&
        endereco.CompareTo(Fim) <= 0;

    /// <summary>Une faixas sobrepostas ou adjacentes, reduzindo a quantidade de entradas no firewall.</summary>
    public static IReadOnlyList<FaixaIp> Mesclar(IEnumerable<FaixaIp> faixas)
    {
        var resultado = new List<FaixaIp>();
        foreach (var familia in faixas.GroupBy(f => f.Inicio.EhIPv4))
        {
            FaixaIp? atual = null;
            foreach (var faixa in familia.OrderBy(f => f.Inicio))
            {
                if (atual is null)
                {
                    atual = faixa;
                    continue;
                }

                if (ParaNumero(faixa.Inicio) <= ParaNumero(atual.Fim) + 1)
                {
                    if (faixa.Fim.CompareTo(atual.Fim) > 0)
                        atual = new FaixaIp(atual.Inicio, faixa.Fim);
                    continue;
                }

                resultado.Add(atual);
                atual = faixa;
            }

            if (atual is not null)
                resultado.Add(atual);
        }

        return resultado;
    }

    /// <summary>Formato aceito pelo Windows Firewall: "a.b.c.d" ou "inicio-fim".</summary>
    public string ParaTextoFirewall() => EhEnderecoUnico ? Inicio.ToString() : $"{Inicio}-{Fim}";

    public override string ToString() => ParaTextoFirewall();

    private static BigInteger ParaNumero(EnderecoIp endereco) =>
        new BigInteger(endereco.ObterBytes16(), isUnsigned: true, isBigEndian: true) & MascaraFamilia(endereco.EhIPv4);

    private static EnderecoIp DeNumero(BigInteger numero, bool ehIPv4)
    {
        var tamanho = ehIPv4 ? 4 : 16;
        var bytes = new byte[tamanho];
        var origem = numero.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (numero.IsZero)
            origem = [];
        Array.Copy(origem, 0, bytes, tamanho - origem.Length, origem.Length);
        return EnderecoIp.Criar(new IPAddress(bytes));
    }

    private static BigInteger MascaraFamilia(bool ehIPv4) =>
        ehIPv4 ? (BigInteger.One << 32) - 1 : (BigInteger.One << 128) - 1;

    private static bool TentarMascaraParaPrefixo(IPAddress mascara, out int prefixo)
    {
        prefixo = 0;
        var fimDosUns = false;
        foreach (var octeto in mascara.GetAddressBytes())
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                var ligado = (octeto & (1 << bit)) != 0;
                if (ligado && fimDosUns)
                    return false;
                if (ligado)
                    prefixo++;
                else
                    fimDosUns = true;
            }
        }

        return true;
    }
}
