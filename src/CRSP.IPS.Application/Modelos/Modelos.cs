using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Application.Modelos;

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

/// <summary>Trecho de uma consulta (rolagem infinita): os itens pedidos e o total de registros.</summary>
public sealed record Fatia<T>(IReadOnlyList<T> Itens, int Total);

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
    public int Pular { get; init; }
    public int Quantidade { get; init; } = 50;
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

/// <summary>Periodo do filtro do dashboard. Todos os cards, graficos e tabelas usam o mesmo periodo.</summary>
public enum PeriodoDashboard
{
    Hoje = 0,
    Dias7 = 7,
    Dias14 = 14,
    Dias30 = 30,
    Dias90 = 90,
    Dias120 = 120,
    Dias180 = 180,
    Dias360 = 360,
    Tudo = -1
}

public enum GranularidadeSerie
{
    Hora,
    Dia,
    Semana
}

/// <param name="Inicio">Inicio do intervalo no horario local do servidor.</param>
public sealed record PontoSerie(DateTime Inicio, int Quantidade);

/// <summary>Contagem por hora UTC (agregada no banco), base das series do dashboard.</summary>
public sealed record ContagemHora(DateTime HoraUtc, int Quantidade);

public sealed record ResumoBloqueios(int AtivosReais, int AtivosSimulados, int Reais, int Simulados);

public sealed record IpAgressor(string Ip, string? PaisCodigo, int Eventos, int Bloqueios);

public sealed record IndicadoresDashboard(
    PeriodoDashboard Periodo,
    GranularidadeSerie Granularidade,
    int RetencaoEventosDias,
    int BloqueiosAtivos,
    int BloqueiosSimuladosAtivos,
    int BloqueiosNoPeriodo,
    int BloqueiosSimuladosNoPeriodo,
    int EventosNoPeriodo,
    int PaisesNoPeriodo,
    bool ModoSimulacao,
    ModoPoliticaPaises ModoPaises,
    StatusWorker? Worker,
    bool WorkerOnline,
    IReadOnlyList<PontoSerie> SerieBloqueios,
    IReadOnlyList<PontoSerie> SerieEventos,
    IReadOnlyList<ItemRanking> TopPaises,
    IReadOnlyList<IpAgressor> TopIps,
    IReadOnlyList<ItemRanking> TopUrls,
    IReadOnlyList<ItemRanking> EventosPorFonte,
    IReadOnlyList<Bloqueio> BloqueiosRecentes);

/// <summary>Filtros do Monitor. Nulo = todos.</summary>
public sealed record FiltroEventos(TipoFonte? Fonte = null, string? PaisCodigo = null, int? CodigoStatus = null);

/// <summary>Valores presentes nos eventos registrados, para preencher os filtros do Monitor.</summary>
public sealed record OpcoesFiltroEventos(IReadOnlyList<string> Paises, IReadOnlyList<int> CodigosStatus);

/// <summary>Linha lida de um CSV de importacao (lista branca/negra).</summary>
public sealed record LinhaImportacao(int Numero, string Faixa, string? Descricao);

/// <param name="Mensagem">Texto em pt-BR, usado tambem como chave de traducao.</param>
public sealed record ErroImportacao(int Linha, string Valor, string Mensagem);

public sealed record ResultadoImportacao(int Adicionadas, int Duplicadas, IReadOnlyList<ErroImportacao> Erros);

public sealed record DadosListaExterna(
    string Nome,
    string? Descricao,
    IReadOnlyList<string> Urls,
    FormatoListaExterna Formato,
    int IntervaloHoras,
    int LimiteEntradas,
    ModoListaExterna Modo);

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
