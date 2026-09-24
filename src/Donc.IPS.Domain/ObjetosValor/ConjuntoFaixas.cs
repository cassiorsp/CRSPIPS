namespace Donc.IPS.Domain.ObjetosValor;

/// <summary>Colecao imutavel de faixas (ja mescladas) com verificacao de pertinencia.</summary>
public sealed class ConjuntoFaixas
{
    public ConjuntoFaixas(IEnumerable<FaixaIp> faixas) => Faixas = FaixaIp.Mesclar(faixas);

    public static ConjuntoFaixas Vazio { get; } = new([]);

    public IReadOnlyList<FaixaIp> Faixas { get; }

    public bool Contem(EnderecoIp endereco) => Faixas.Any(f => f.Contem(endereco));
}
