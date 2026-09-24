using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Modelos;

/// <summary>Resultado de operacao. A mensagem de erro e o texto em pt-BR, usado tambem como chave de traducao.</summary>
public record Resultado(bool Sucesso, string? Erro)
{
    public static Resultado Ok() => new(true, null);
    public static Resultado Falha(string erro) => new(false, erro);
}

public sealed record Resultado<T>(bool Sucesso, string? Erro, T? Valor) : Resultado(Sucesso, Erro)
{
    public static Resultado<T> Ok(T valor) => new(true, null, valor);
    public static new Resultado<T> Falha(string erro) => new(false, erro, default);
}

public sealed record Pagina<T>(IReadOnlyList<T> Itens, int Total, int PaginaAtual, int TamanhoPagina)
{
    public int TotalPaginas => TamanhoPagina <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanhoPagina));
}

public sealed record FiltroBloqueios
{
    /// <summary>IP exato, CIDR/intervalo ou trecho do IP.</summary>
    public string? Busca { get; init; }
    public StatusBloqueio? Status { get; init; }
    public OrigemBloqueio? Origem { get; init; }
    public string? PaisCodigo { get; init; }
    public int? RegraId { get; init; }
    public bool? Simulado { get; init; }
    public DateTime? DeUtc { get; init; }
    public DateTime? AteUtc { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 50;
}

public sealed record EventoDetectado(
    EnderecoIp Ip,
    TipoFonte Fonte,
    DateTime OcorridoEmUtc,
    string? Metodo = null,
    string? Url = null,
    int? CodigoStatus = null,
    string? UserAgent = null,
    string? MotivoHttpErr = null,
    int? IdEventoWindows = null,
    string? Detalhe = null,
    string? Site = null);

/// <summary>Grupos de regras de bloqueio no firewall, cada um com prefixo proprio (facil de identificar no wf.msc).</summary>
public enum ConjuntoRegrasFirewall
{
    /// <summary>Bloqueios do motor e lista negra manual: CRSPIPS_Bloqueio_NNN.</summary>
    Bloqueios = 1,

    /// <summary>Listas externas (Spamhaus, DShield...): CRSPIPS_ListaExterna_NNN.</summary>
    ListasExternas = 2
}

public sealed record PlanoRestricaoPaises(
    ModoPoliticaPaises Modo,
    IReadOnlyList<int> Portas,
    IReadOnlyList<FaixaIp> FaixasPaises,
    IReadOnlyList<FaixaIp> FaixasSempreLiberadas);

public sealed record StatusBaseGeo(
    string CaminhoCidade,
    bool CidadeEncontrada,
    DateTime? CidadeAtualizadaEm,
    string CaminhoAsn,
    bool AsnEncontrada,
    string CaminhoPaisesCsv,
    bool PaisesCsvEncontrado);

/// <param name="Posicao">Bytes lidos (arquivos) ou ultimo EventRecordID (Event Log).</param>
public sealed record LeituraFonte(TipoFonte? Fonte, string Origem, long Posicao, DateTime AtualizadaEmUtc);

/// <summary>Mensagem em pt-BR, usada tambem como chave de traducao.</summary>
public sealed record ResultadoAtualizacaoGeo(bool Sucesso, int BasesAtualizadas, string Mensagem);

public sealed record ItemRanking(string Rotulo, int Quantidade, string? Complemento = null);

public sealed record PontoSerie(string Rotulo, int Quantidade);

public sealed record IndicadoresDashboard(
    int BloqueiosAtivos,
    int BloqueiosSimuladosAtivos,
    int BloqueadosHoje,
    int BloqueadosHojeSimulados,
    int EventosUltimas24h,
    int PaisesUltimas24h,
    bool ModoSimulacao,
    ModoPoliticaPaises ModoPaises,
    StatusWorker? Worker,
    bool WorkerOnline,
    IReadOnlyList<PontoSerie> SerieBloqueios,
    IReadOnlyList<PontoSerie> SerieEventos,
    IReadOnlyList<ItemRanking> TopPaises,
    IReadOnlyList<ItemRanking> TopIps,
    IReadOnlyList<ItemRanking> TopUrls,
    IReadOnlyList<ItemRanking> EventosPorFonte,
    IReadOnlyList<Bloqueio> BloqueiosRecentes);

public sealed record DetalheIp(
    string Ip,
    LocalizacaoIp? Localizacao,
    bool Protegido,
    string? MotivoProtecao,
    bool NaListaNegra,
    IReadOnlyList<(string Lista, string? Referencia)> ListasExternas,
    Bloqueio? BloqueioAtivo,
    IReadOnlyList<Bloqueio> Historico,
    IReadOnlyList<EventoResumo> Eventos);

public sealed record EventoResumo(
    long Id,
    string Ip,
    TipoFonte Fonte,
    string? Regra,
    DateTime OcorridoEmUtc,
    string? Metodo,
    string? Url,
    int? CodigoStatus,
    string? UserAgent,
    string? Detalhe,
    string? PaisCodigo,
    string? Site);

public sealed record DadosRegra(
    string Nome,
    string? Descricao,
    TipoFonte Fonte,
    TipoCriterio Criterio,
    string Padrao,
    int LimiteOcorrencias,
    int JanelaSegundos,
    bool Ativa);

public sealed record DadosConfiguracaoMotor(
    bool ModoSimulacao,
    string TemposBloqueio,
    int JanelaReincidenciaDias,
    bool MonitorarLogIis,
    string CaminhoLogIis,
    bool MonitorarHttpErr,
    string CaminhoHttpErr,
    bool MonitorarEventosWindows,
    int RetencaoEventosDias);

public sealed record DadosPoliticaPaises(
    ModoPoliticaPaises Modo,
    AplicacaoPoliticaPaises Aplicacao,
    IReadOnlyList<string> Paises,
    IReadOnlyList<int> Portas);
