namespace Donc.IPS.Domain.Entidades;

/// <summary>
/// Credenciais MaxMind e estado da atualizacao automatica das bases GeoLite2. Registro unico (Id = 1).
/// A chave de licenca e guardada ja protegida (DPAPI); a entidade nunca ve o valor em texto puro.
/// </summary>
public class ConfiguracaoGeoIp
{
    public const int IdUnico = 1;
    public static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromHours(24);

    public int Id { get; private set; } = IdUnico;
    public string? ContaId { get; private set; }
    public string? ChaveProtegida { get; private set; }
    public string? FinalChave { get; private set; }
    public bool AtualizacaoAutomatica { get; private set; } = true;
    public bool AtualizacaoSolicitada { get; private set; }
    public DateTime? UltimaVerificacaoEm { get; private set; }
    public DateTime? UltimaAtualizacaoEm { get; private set; }
    public bool UltimoResultadoSucesso { get; private set; }
    public string? UltimoResultado { get; private set; }

    public bool PossuiCredenciais => !string.IsNullOrWhiteSpace(ContaId) && !string.IsNullOrWhiteSpace(ChaveProtegida);

    /// <param name="chaveProtegida">Null mantem a chave atual.</param>
    public void AlterarCredenciais(string contaId, string? chaveProtegida, string? finalChave, bool atualizacaoAutomatica)
    {
        contaId = contaId?.Trim() ?? string.Empty;
        if (contaId.Length == 0 || !contaId.All(char.IsAsciiDigit))
            throw new ArgumentException("O ID da conta MaxMind deve conter apenas números.");
        if (chaveProtegida is null && string.IsNullOrWhiteSpace(ChaveProtegida))
            throw new ArgumentException("Informe a chave de licença da MaxMind.");

        ContaId = contaId;
        if (chaveProtegida is not null)
        {
            ChaveProtegida = chaveProtegida;
            FinalChave = finalChave;
        }

        AtualizacaoAutomatica = atualizacaoAutomatica;
        AtualizacaoSolicitada = true;
    }

    public void RemoverCredenciais()
    {
        ContaId = null;
        ChaveProtegida = null;
        FinalChave = null;
        AtualizacaoSolicitada = false;
    }

    public void SolicitarAtualizacao()
    {
        if (!PossuiCredenciais)
            throw new InvalidOperationException("Cadastre as credenciais da MaxMind primeiro.");
        AtualizacaoSolicitada = true;
    }

    public bool DeveVerificar(DateTime agoraUtc) =>
        PossuiCredenciais &&
        (AtualizacaoSolicitada || (AtualizacaoAutomatica && (UltimaVerificacaoEm is null || agoraUtc - UltimaVerificacaoEm >= IntervaloVerificacao)));

    public void RegistrarVerificacao(DateTime agoraUtc, bool sucesso, int basesAtualizadas, string resultado)
    {
        UltimaVerificacaoEm = agoraUtc;
        UltimoResultadoSucesso = sucesso;
        UltimoResultado = resultado.Length > 500 ? resultado[..500] : resultado;
        AtualizacaoSolicitada = false;
        if (sucesso && basesAtualizadas > 0)
            UltimaAtualizacaoEm = agoraUtc;
    }
}
