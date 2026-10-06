namespace CRSP.IPS.Application.Modelos;

/// <summary>Consumo instantaneo de um processo w3wp, lido pelo Worker.</summary>
public sealed record AmostraProcessoIis(string Pool, int ProcessoId, long MemoriaPrivadaBytes, double CpuPercentual);

public sealed record SitePool(string Nome, string Pool);

/// <summary>Agregado de requisicoes (por endpoint ou por site, conforme a consulta).</summary>
public sealed record LinhaRequisicoes(
    string Site,
    string Metodo,
    string Endpoint,
    int Total,
    int Sucesso,
    int Redirecionamento,
    int ErroCliente,
    int ErroServidor,
    long TempoTotalMs,
    int TempoMaximoMs)
{
    public double TempoMedioMs => Total == 0 ? 0 : (double)TempoTotalMs / Total;

    /// <summary>Percentual de respostas 5xx (erro do servidor) sobre o total.</summary>
    public double PercentualErro => Total == 0 ? 0 : 100d * ErroServidor / Total;
}

/// <summary>Requisicoes de uma hora UTC, base das series de requisicoes.</summary>
public sealed record RequisicoesHora(DateTime HoraUtc, int Sucesso, int ErroCliente, int ErroServidor);

/// <param name="Inicio">Inicio do intervalo no horario local do servidor.</param>
public sealed record PontoRequisicoes(DateTime Inicio, int Sucesso, int ErroCliente, int ErroServidor);

public sealed record TotaisRequisicoes(int Total, int Sucesso, int Redirecionamento, int ErroCliente, int ErroServidor, double TempoMedioMs, int TempoMaximoMs)
{
    public double PercentualErro => Total == 0 ? 0 : 100d * ErroServidor / Total;
    public double PercentualSucesso => Total == 0 ? 0 : 100d * (Sucesso + Redirecionamento) / Total;
}

public sealed record PainelEndpoints(
    PeriodoDashboard Periodo,
    GranularidadeSerie Granularidade,
    string? Site,
    IReadOnlyList<string> Sites,
    TotaisRequisicoes Totais,
    IReadOnlyList<PontoRequisicoes> Serie,
    IReadOnlyList<LinhaRequisicoes> Endpoints,
    bool WorkerOnline,
    bool HaDados);

/// <summary>Consumo de um pool no periodo. Memoria em bytes.</summary>
public sealed record ResumoPool(string Pool, long MemoriaMinima, long MemoriaMaxima, long MemoriaMedia, double CpuMedia, double CpuMaxima, int ProcessosMaximo, DateTime UltimaAmostraUtc);

public sealed record ResumoSiteIis(
    string Site,
    string? Pool,
    TotaisRequisicoes Requisicoes,
    ResumoPool? Consumo);

/// <param name="Inicio">Inicio do intervalo no horario local do servidor.</param>
public sealed record PontoMemoria(DateTime Inicio, long Minima, long Maxima, long Media, double CpuMedia);

public sealed record PainelIis(
    PeriodoDashboard Periodo,
    GranularidadeSerie Granularidade,
    bool SerieEmJanelas,
    IReadOnlyList<ResumoSiteIis> Sites,
    IReadOnlyList<ResumoPool> Pools,
    string? PoolSelecionado,
    IReadOnlyList<PontoMemoria> SerieMemoria,
    IReadOnlyList<PontoRequisicoes> SerieRequisicoes,
    bool WorkerOnline);
