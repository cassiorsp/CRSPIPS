namespace CRSP.IPS.Domain.Entidades;

/// <summary>
/// Contagem de requisicoes do IIS por hora, site, metodo e endpoint (URL normalizada). Alimentada com TODAS as
/// requisicoes do log do IIS (nao so as suspeitas), por isso guarda somente agregados.
/// </summary>
public class MetricaEndpoint
{
    public long Id { get; private set; }

    /// <summary>Inicio da hora (UTC).</summary>
    public DateTime HoraUtc { get; private set; }
    public string Site { get; private set; } = string.Empty;
    public string Metodo { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;

    public int Total { get; private set; }
    public int Sucesso { get; private set; }
    public int Redirecionamento { get; private set; }
    public int ErroCliente { get; private set; }
    public int ErroServidor { get; private set; }

    public long TempoTotalMs { get; private set; }
    public int TempoMaximoMs { get; private set; }

    protected MetricaEndpoint() { }

    public static MetricaEndpoint Criar(DateTime horaUtc, string site, string metodo, string endpoint) => new()
    {
        HoraUtc = horaUtc,
        Site = site,
        Metodo = metodo,
        Endpoint = endpoint
    };

    /// <summary>Soma uma requisicao. Status nulo (linha sem sc-status) conta como sucesso.</summary>
    public void Registrar(int? status, int? tempoMs)
    {
        Total++;
        switch (status)
        {
            case >= 500: ErroServidor++; break;
            case >= 400: ErroCliente++; break;
            case >= 300: Redirecionamento++; break;
            default: Sucesso++; break;
        }

        if (tempoMs is { } tempo)
        {
            TempoTotalMs += tempo;
            TempoMaximoMs = Math.Max(TempoMaximoMs, tempo);
        }
    }

    public void Somar(MetricaEndpoint outra)
    {
        Total += outra.Total;
        Sucesso += outra.Sucesso;
        Redirecionamento += outra.Redirecionamento;
        ErroCliente += outra.ErroCliente;
        ErroServidor += outra.ErroServidor;
        TempoTotalMs += outra.TempoTotalMs;
        TempoMaximoMs = Math.Max(TempoMaximoMs, outra.TempoMaximoMs);
    }
}

/// <summary>
/// Consumo de um application pool do IIS (soma dos processos w3wp do pool) em janelas de 5 minutos.
/// Memoria em bytes de memoria privada.
/// </summary>
public class MetricaProcessoIis
{
    public long Id { get; private set; }

    /// <summary>Inicio da janela de 5 minutos (UTC).</summary>
    public DateTime InicioUtc { get; private set; }
    public string Pool { get; private set; } = string.Empty;

    public int Amostras { get; private set; }
    public long MemoriaMinima { get; private set; }
    public long MemoriaMaxima { get; private set; }
    public long MemoriaSoma { get; private set; }
    public double CpuMaxima { get; private set; }
    public double CpuSoma { get; private set; }
    public int ProcessosMaximo { get; private set; }

    protected MetricaProcessoIis() { }

    public static MetricaProcessoIis Criar(DateTime inicioUtc, string pool) => new()
    {
        InicioUtc = inicioUtc,
        Pool = pool
    };

    public void Registrar(long memoria, double cpuPercentual, int processos)
    {
        MemoriaMinima = Amostras == 0 ? memoria : Math.Min(MemoriaMinima, memoria);
        MemoriaMaxima = Math.Max(MemoriaMaxima, memoria);
        MemoriaSoma += memoria;
        CpuMaxima = Math.Max(CpuMaxima, cpuPercentual);
        CpuSoma += cpuPercentual;
        ProcessosMaximo = Math.Max(ProcessosMaximo, processos);
        Amostras++;
    }

    public long MemoriaMedia => Amostras == 0 ? 0 : MemoriaSoma / Amostras;
    public double CpuMedia => Amostras == 0 ? 0 : CpuSoma / Amostras;
}

/// <summary>Site do IIS e o application pool que o atende. Gravado pelo Worker (o painel nao le o applicationHost.config).</summary>
public class SiteIis
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Pool { get; private set; } = string.Empty;
    public DateTime AtualizadoEm { get; private set; }

    protected SiteIis() { }

    public static SiteIis Criar(string nome, string pool, DateTime agoraUtc) => new()
    {
        Nome = nome,
        Pool = pool,
        AtualizadoEm = agoraUtc
    };

    public void Atualizar(string pool, DateTime agoraUtc)
    {
        Pool = pool;
        AtualizadoEm = agoraUtc;
    }
}
