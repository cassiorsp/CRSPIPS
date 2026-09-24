using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Domain.Entidades;

/// <summary>
/// Estado desejado de bloqueio de um IP. O Worker le os bloqueios ativos e reconcilia o Windows Firewall.
/// Bloqueios simulados sao registrados, mas nunca aplicados no firewall.
/// </summary>
public class Bloqueio
{
    public int Id { get; private set; }
    public string Ip { get; private set; } = string.Empty;
    public string IpChave { get; private set; } = string.Empty;
    public OrigemBloqueio Origem { get; private set; }
    public StatusBloqueio Status { get; private set; }
    public bool Simulado { get; private set; }
    public int? RegraId { get; private set; }
    public string Motivo { get; private set; } = string.Empty;
    public int Ocorrencias { get; private set; }
    public int NivelReincidencia { get; private set; }
    public DateTime BloqueadoEm { get; private set; }
    public DateTime? ExpiraEm { get; private set; }
    public DateTime? AplicadoNoFirewallEm { get; private set; }
    public DateTime? EncerradoEm { get; private set; }
    public string? EncerradoPor { get; private set; }
    public string? PaisCodigo { get; private set; }
    public string? PaisNome { get; private set; }
    public string? Cidade { get; private set; }
    public int? Asn { get; private set; }
    public string? Organizacao { get; private set; }
    public string CriadoPor { get; private set; } = string.Empty;
    public string? Comentario { get; private set; }

    public RegraDeteccao? Regra { get; private set; }

    protected Bloqueio() { }

    public static Bloqueio Criar(
        EnderecoIp ip,
        OrigemBloqueio origem,
        string motivo,
        DateTime agoraUtc,
        TimeSpan? duracao,
        int nivelReincidencia,
        bool simulado,
        string criadoPor,
        int ocorrencias = 1,
        int? regraId = null,
        string? comentario = null,
        LocalizacaoIp? localizacao = null)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo obrigatorio.", nameof(motivo));

        var bloqueio = new Bloqueio
        {
            Ip = ip.ToString(),
            IpChave = ip.ObterChaveOrdenavel(),
            Origem = origem,
            Status = StatusBloqueio.Ativo,
            Simulado = simulado,
            RegraId = regraId,
            Motivo = motivo,
            Ocorrencias = Math.Max(1, ocorrencias),
            NivelReincidencia = Math.Max(1, nivelReincidencia),
            BloqueadoEm = agoraUtc,
            ExpiraEm = duracao.HasValue ? agoraUtc.Add(duracao.Value) : null,
            CriadoPor = criadoPor,
            Comentario = comentario
        };
        bloqueio.DefinirLocalizacao(localizacao);
        return bloqueio;
    }

    public bool EhPermanente => ExpiraEm is null;

    public bool EstaPendenteNoFirewall => Status == StatusBloqueio.Ativo && !Simulado && AplicadoNoFirewallEm is null;

    public bool DeveExpirar(DateTime agoraUtc) => Status == StatusBloqueio.Ativo && ExpiraEm <= agoraUtc;

    public void Expirar(DateTime agoraUtc)
    {
        if (Status != StatusBloqueio.Ativo)
            return;
        Status = StatusBloqueio.Expirado;
        EncerradoEm = agoraUtc;
        EncerradoPor = "CRSPIPS";
    }

    public void Liberar(DateTime agoraUtc, string usuario, string? comentario)
    {
        if (Status != StatusBloqueio.Ativo)
            throw new InvalidOperationException("Somente bloqueios ativos podem ser liberados.");
        Status = StatusBloqueio.Liberado;
        EncerradoEm = agoraUtc;
        EncerradoPor = usuario;
        if (!string.IsNullOrWhiteSpace(comentario))
            Comentario = string.IsNullOrWhiteSpace(Comentario) ? comentario : $"{Comentario} | {comentario}";
    }

    public void AlterarExpiracao(DateTime? novaExpiracaoUtc, DateTime agoraUtc)
    {
        if (Status != StatusBloqueio.Ativo)
            throw new InvalidOperationException("Somente bloqueios ativos podem ter a expiracao alterada.");
        if (novaExpiracaoUtc.HasValue && novaExpiracaoUtc <= agoraUtc)
            throw new ArgumentException("A nova expiracao deve ser futura.", nameof(novaExpiracaoUtc));
        ExpiraEm = novaExpiracaoUtc;
    }

    public void MarcarAplicadoNoFirewall(DateTime agoraUtc) => AplicadoNoFirewallEm ??= agoraUtc;

    public void DefinirLocalizacao(LocalizacaoIp? localizacao)
    {
        if (localizacao is null)
            return;
        PaisCodigo = localizacao.PaisCodigo;
        PaisNome = localizacao.PaisNome;
        Cidade = localizacao.Cidade;
        Asn = localizacao.Asn;
        Organizacao = localizacao.Organizacao;
    }
}
