namespace CRSP.IPS.Domain.ObjetosValor;

public sealed record LocalizacaoIp(
    string? PaisCodigo,
    string? PaisNome,
    string? Cidade,
    int? Asn,
    string? Organizacao);
