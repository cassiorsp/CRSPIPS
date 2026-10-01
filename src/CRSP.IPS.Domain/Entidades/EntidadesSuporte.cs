namespace CRSP.IPS.Domain.Entidades;

public class RegistroAuditoria
{
    public long Id { get; private set; }
    public DateTime OcorridoEm { get; private set; }
    public string Usuario { get; private set; } = string.Empty;
    public string? IpOrigem { get; private set; }
    public string Acao { get; private set; } = string.Empty;
    public string Alvo { get; private set; } = string.Empty;
    public string? Detalhe { get; private set; }

    protected RegistroAuditoria() { }

    public static RegistroAuditoria Criar(DateTime agoraUtc, string usuario, string? ipOrigem, string acao, string alvo, string? detalhe) => new()
    {
        OcorridoEm = agoraUtc,
        Usuario = usuario,
        IpOrigem = ipOrigem,
        Acao = acao,
        Alvo = alvo,
        Detalhe = detalhe
    };
}

/// <summary>Posicao de leitura de um arquivo de log (bytes) ou canal do Event Log (EventRecordID).</summary>
public class PosicaoLeitura
{
    public string Chave { get; private set; } = string.Empty;
    public long Posicao { get; private set; }
    public DateTime AtualizadaEm { get; private set; }

    protected PosicaoLeitura() { }

    public static PosicaoLeitura Criar(string chave, long posicao, DateTime agoraUtc) => new()
    {
        Chave = chave,
        Posicao = posicao,
        AtualizadaEm = agoraUtc
    };

    public void Atualizar(long posicao, DateTime agoraUtc)
    {
        Posicao = posicao;
        AtualizadaEm = agoraUtc;
    }
}

/// <summary>Heartbeat do Worker, exibido no Dashboard. Registro unico (Id = 1).</summary>
public class StatusWorker
{
    public const int IdUnico = 1;

    public int Id { get; private set; } = IdUnico;
    public string Maquina { get; private set; } = string.Empty;
    public string Versao { get; private set; } = string.Empty;
    public DateTime IniciadoEm { get; private set; }
    public DateTime UltimoSinalEm { get; private set; }
    public DateTime? UltimaSincronizacaoEm { get; private set; }
    /// <summary>Bloqueios (motor, manuais e lista negra): regras CRSPIPS_LOGIIS/HTTPERR/EVENTOWINDOWS/MANUAL/LISTANEGRA.</summary>
    public int RegrasNoFirewall { get; private set; }
    public int EnderecosNoFirewall { get; private set; }

    /// <summary>Listas externas em modo Ativa: regras CRSPIPS_LISTAEXTERNA.</summary>
    public int RegrasListasExternas { get; private set; }
    public int EnderecosListasExternas { get; private set; }
    public long EventosProcessados { get; private set; }
    public string? UltimoErro { get; private set; }
    public DateTime? UltimoErroEm { get; private set; }
    public string? ResumoPoliticaPaises { get; private set; }

    public static StatusWorker Iniciar(string maquina, string versao, DateTime agoraUtc) => new()
    {
        Maquina = maquina,
        Versao = versao,
        IniciadoEm = agoraUtc,
        UltimoSinalEm = agoraUtc
    };

    public void Reiniciar(string maquina, string versao, DateTime agoraUtc)
    {
        Maquina = maquina;
        Versao = versao;
        IniciadoEm = agoraUtc;
        UltimoSinalEm = agoraUtc;
        UltimoErro = null;
        UltimoErroEm = null;
    }

    public void RegistrarSinal(DateTime agoraUtc) => UltimoSinalEm = agoraUtc;

    public void RegistrarEventosProcessados(int quantidade) => EventosProcessados += quantidade;

    public void RegistrarSincronizacao(DateTime agoraUtc, int regras, int enderecos)
    {
        UltimaSincronizacaoEm = agoraUtc;
        RegrasNoFirewall = regras;
        EnderecosNoFirewall = enderecos;
    }

    public void RegistrarListasExternas(int regras, int enderecos)
    {
        RegrasListasExternas = regras;
        EnderecosListasExternas = enderecos;
    }

    public void RegistrarPoliticaPaises(string? resumo) => ResumoPoliticaPaises = resumo;

    public void RegistrarErro(string erro, DateTime agoraUtc)
    {
        UltimoErro = erro.Length > 1000 ? erro[..1000] : erro;
        UltimoErroEm = agoraUtc;
    }

    public bool EstaOnline(DateTime agoraUtc) => agoraUtc - UltimoSinalEm < TimeSpan.FromSeconds(30);
}

/// <summary>
/// Regra do Windows Firewall de terceiros desativada pela politica de paises (modo FirewallPorPortas).
/// Guardada para ser reativada quando a politica for desligada.
/// </summary>
public class RegraFirewallDesativada
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public DateTime DesativadaEm { get; private set; }

    protected RegraFirewallDesativada() { }

    public static RegraFirewallDesativada Criar(string nome, DateTime agoraUtc) => new()
    {
        Nome = nome,
        DesativadaEm = agoraUtc
    };
}
