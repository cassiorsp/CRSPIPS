using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Domain.Entidades;

/// <summary>
/// Lista publica de IPs maliciosos baixada periodicamente (Spamhaus DROP, DShield, IPsum...).
/// Separada da lista negra manual: uma atualizacao nunca altera o que o administrador cadastrou.
/// </summary>
public class ListaExterna
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    /// <summary>Uma ou mais URLs HTTPS separadas por quebra de linha (ex.: Spamhaus IPv4 e IPv6).</summary>
    public string Urls { get; private set; } = string.Empty;
    public FormatoListaExterna Formato { get; private set; }
    public int IntervaloHoras { get; private set; }
    public int LimiteEntradas { get; private set; }
    public ModoListaExterna Modo { get; private set; }
    public bool AtualizacaoSolicitada { get; private set; }
    public DateTime? UltimaVerificacaoEm { get; private set; }
    public DateTime? UltimaAtualizacaoEm { get; private set; }
    public int Quantidade { get; private set; }
    public int Removidas { get; private set; }
    public string? UltimoErro { get; private set; }

    /// <summary>Cadastrada pelo administrador (pode ser excluida). As do catalogo sao recriadas a cada inicializacao.</summary>
    public bool Personalizada { get; private set; }

    protected ListaExterna() { }

    public static ListaExterna Criar(
        string nome, string? descricao, IEnumerable<string> urls, FormatoListaExterna formato,
        int intervaloHoras, int limiteEntradas, ModoListaExterna modo, bool personalizada = false)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Informe o nome da lista.");
        var listaUrls = urls.Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
        if (listaUrls.Count == 0 || listaUrls.Any(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Informe ao menos uma URL HTTPS válida.");
        if (intervaloHoras is < 1 or > 168)
            throw new ArgumentException("O intervalo deve estar entre 1 e 168 horas.");

        return new ListaExterna
        {
            Nome = nome.Trim(),
            Descricao = descricao,
            Urls = string.Join('\n', listaUrls),
            Formato = formato,
            IntervaloHoras = intervaloHoras,
            LimiteEntradas = Math.Max(1, limiteEntradas),
            Modo = modo,
            AtualizacaoSolicitada = modo != ModoListaExterna.Desativada,
            Personalizada = personalizada
        };
    }

    public IReadOnlyList<string> ObterUrls() => Urls.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Baixada periodicamente (modos Avaliacao e Ativa).</summary>
    public bool EstaEmUso => Modo != ModoListaExterna.Desativada;

    public DateTime? ProximaAtualizacaoEm => EstaEmUso ? (UltimaVerificacaoEm?.AddHours(IntervaloHoras) ?? DateTime.MinValue) : null;

    public bool DeveAtualizar(DateTime agoraUtc) =>
        EstaEmUso && (AtualizacaoSolicitada || UltimaVerificacaoEm is null || agoraUtc >= UltimaVerificacaoEm.Value.AddHours(IntervaloHoras));

    public void DefinirModo(ModoListaExterna modo)
    {
        Modo = modo;
        if (EstaEmUso && Quantidade == 0)
            AtualizacaoSolicitada = true;
    }

    public void SolicitarAtualizacao() => AtualizacaoSolicitada = true;

    public void RegistrarSucesso(DateTime agoraUtc, int quantidade, int removidas)
    {
        UltimaVerificacaoEm = agoraUtc;
        UltimaAtualizacaoEm = agoraUtc;
        Quantidade = quantidade;
        Removidas = removidas;
        UltimoErro = null;
        AtualizacaoSolicitada = false;
    }

    /// <summary>Falha no download ou conteudo invalido: as entradas anteriores continuam valendo.</summary>
    public void RegistrarFalha(DateTime agoraUtc, string erro)
    {
        UltimaVerificacaoEm = agoraUtc;
        UltimoErro = erro.Length > 500 ? erro[..500] : erro;
        AtualizacaoSolicitada = false;
    }
}

public class EntradaListaExterna
{
    public long Id { get; private set; }
    public int ListaExternaId { get; private set; }
    public string Faixa { get; private set; } = string.Empty;
    public string InicioChave { get; private set; } = string.Empty;
    public string FimChave { get; private set; } = string.Empty;

    /// <summary>Identificador na fonte (ex.: SBL256894 na Spamhaus), quando existir.</summary>
    public string? Referencia { get; private set; }

    protected EntradaListaExterna() { }

    public static EntradaListaExterna Criar(int listaExternaId, FaixaIp faixa, string? referencia) => new()
    {
        ListaExternaId = listaExternaId,
        Faixa = faixa.ParaTextoFirewall(),
        InicioChave = faixa.Inicio.ObterChaveOrdenavel(),
        FimChave = faixa.Fim.ObterChaveOrdenavel(),
        Referencia = referencia is { Length: > 50 } ? referencia[..50] : referencia
    };

    public FaixaIp ObterFaixa() => FaixaIp.Converter(Faixa);
}

/// <summary>
/// Trafego real (qualquer requisicao ou evento) vindo de um IP que esta em uma lista externa.
/// Serve para avaliar uma lista em simulacao: coincidencias com acessos legitimos indicam risco de falso positivo.
/// </summary>
public class CoincidenciaListaExterna
{
    public long Id { get; private set; }
    public int ListaExternaId { get; private set; }
    public string Ip { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }
    public DateTime PrimeiraEm { get; private set; }
    public DateTime UltimaEm { get; private set; }
    public TipoFonte UltimaFonte { get; private set; }
    public string? UltimoSite { get; private set; }
    public string? UltimaUrl { get; private set; }
    public int? UltimoCodigoStatus { get; private set; }

    protected CoincidenciaListaExterna() { }

    public static CoincidenciaListaExterna Criar(int listaExternaId, string ip, DateTime momentoUtc) => new()
    {
        ListaExternaId = listaExternaId,
        Ip = ip,
        PrimeiraEm = momentoUtc,
        UltimaEm = momentoUtc
    };

    public void Registrar(int quantidade, DateTime momentoUtc, TipoFonte fonte, string? site, string? url, int? codigoStatus)
    {
        Quantidade += quantidade;
        if (momentoUtc < UltimaEm)
            return;
        UltimaEm = momentoUtc;
        UltimaFonte = fonte;
        UltimoSite = site is { Length: > 200 } ? site[..200] : site;
        UltimaUrl = url is { Length: > 500 } ? url[..500] : url;
        UltimoCodigoStatus = codigoStatus;
    }
}
