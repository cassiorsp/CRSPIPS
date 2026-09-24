using Donc.IPS.Domain.Enums;

namespace Donc.IPS.Domain.Entidades;

/// <summary>
/// Amostra de evento suspeito que casou com alguma regra. Nao e gravado um registro por requisicao,
/// somente os eventos relevantes, para auditoria e para a tela de detalhe do IP.
/// </summary>
public class EventoSeguranca
{
    public long Id { get; private set; }
    public string Ip { get; private set; } = string.Empty;
    public TipoFonte Fonte { get; private set; }
    public int? RegraId { get; private set; }
    public DateTime OcorridoEm { get; private set; }
    public string? Metodo { get; private set; }
    public string? Url { get; private set; }
    public int? CodigoStatus { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Detalhe { get; private set; }
    public string? PaisCodigo { get; private set; }

    /// <summary>Site do IIS (nome ou hostname) que recebeu a requisicao. Nulo para eventos do Windows.</summary>
    public string? Site { get; private set; }

    protected EventoSeguranca() { }

    public static EventoSeguranca Criar(
        string ip,
        TipoFonte fonte,
        int? regraId,
        DateTime ocorridoEmUtc,
        string? metodo,
        string? url,
        int? codigoStatus,
        string? userAgent,
        string? detalhe,
        string? paisCodigo,
        string? site = null) => new()
    {
        Ip = ip,
        Fonte = fonte,
        RegraId = regraId,
        OcorridoEm = ocorridoEmUtc,
        Metodo = Limitar(metodo, 16),
        Url = Limitar(url, 2048),
        CodigoStatus = codigoStatus,
        UserAgent = Limitar(userAgent, 512),
        Detalhe = Limitar(detalhe, 512),
        PaisCodigo = paisCodigo,
        Site = Limitar(site, 200)
    };

    public void DefinirPais(string? paisCodigo) => PaisCodigo = paisCodigo;

    private static string? Limitar(string? valor, int tamanho) =>
        valor is null || valor.Length <= tamanho ? valor : valor[..tamanho];
}
