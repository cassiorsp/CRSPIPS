using System.Net;
using System.Net.Sockets;

namespace Donc.IPS.Domain.ObjetosValor;

/// <summary>
/// Endereco IPv4 ou IPv6 normalizado. IPv4 mapeado em IPv6 (::ffff:a.b.c.d) vira IPv4.
/// A chave ordenavel (hex de 16 bytes) permite comparar e buscar faixas no banco com uma simples comparacao de texto.
/// </summary>
public readonly record struct EnderecoIp : IComparable<EnderecoIp>
{
    private readonly IPAddress? _valor;

    private EnderecoIp(IPAddress valor) => _valor = valor;

    public IPAddress Valor => _valor ?? IPAddress.None;

    public bool EhIPv4 => Valor.AddressFamily == AddressFamily.InterNetwork;

    public static EnderecoIp Criar(IPAddress endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);
        if (endereco.IsIPv4MappedToIPv6)
            return new EnderecoIp(endereco.MapToIPv4());

        if (endereco.AddressFamily == AddressFamily.InterNetworkV6 && endereco.ScopeId != 0)
            endereco = new IPAddress(endereco.GetAddressBytes());

        return new EnderecoIp(endereco);
    }

    public static bool TentarConverter(string? texto, out EnderecoIp endereco)
    {
        endereco = default;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        if (!IPAddress.TryParse(texto.Trim(), out var ip))
            return false;

        if (ip.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return false;

        endereco = Criar(ip);
        return true;
    }

    public static EnderecoIp Converter(string texto) =>
        TentarConverter(texto, out var endereco)
            ? endereco
            : throw new FormatException($"Endereco IP invalido: '{texto}'.");

    /// <summary>16 bytes big-endian; IPv4 no formato mapeado ::ffff:a.b.c.d.</summary>
    public byte[] ObterBytes16() =>
        (EhIPv4 ? Valor.MapToIPv6() : Valor).GetAddressBytes();

    /// <summary>Hex de 32 caracteres. Comparacao lexicografica equivale a comparacao numerica.</summary>
    public string ObterChaveOrdenavel() => Convert.ToHexStringLower(ObterBytes16());

    public static EnderecoIp DeBytes16(byte[] bytes)
    {
        if (bytes.Length != 16)
            throw new ArgumentException("Esperados 16 bytes.", nameof(bytes));
        return Criar(new IPAddress(bytes));
    }

    public int CompareTo(EnderecoIp outro) =>
        string.CompareOrdinal(ObterChaveOrdenavel(), outro.ObterChaveOrdenavel());

    public bool Equals(EnderecoIp outro) => Valor.Equals(outro.Valor);

    public override int GetHashCode() => Valor.GetHashCode();

    public override string ToString() => _valor?.ToString() ?? string.Empty;
}
