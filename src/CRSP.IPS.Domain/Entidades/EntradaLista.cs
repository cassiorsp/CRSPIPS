using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Domain.Entidades;

/// <summary>Entrada de lista branca (nunca bloquear) ou negra (bloqueio permanente). Aceita IP, CIDR ou intervalo.</summary>
public class EntradaLista
{
    public int Id { get; private set; }
    public TipoLista Tipo { get; private set; }
    public string Faixa { get; private set; } = string.Empty;
    public string InicioChave { get; private set; } = string.Empty;
    public string FimChave { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public bool Sistema { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public string CriadaPor { get; private set; } = string.Empty;

    protected EntradaLista() { }

    public static EntradaLista Criar(TipoLista tipo, FaixaIp faixa, string textoOriginal, string? descricao, bool sistema, DateTime agoraUtc, string criadaPor) => new()
    {
        Tipo = tipo,
        Faixa = textoOriginal.Trim(),
        InicioChave = faixa.Inicio.ObterChaveOrdenavel(),
        FimChave = faixa.Fim.ObterChaveOrdenavel(),
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
        Sistema = sistema,
        CriadaEm = agoraUtc,
        CriadaPor = criadaPor
    };

    public FaixaIp ObterFaixa() => FaixaIp.Converter(Faixa);
}
