using System.Collections.Concurrent;
using System.Text;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Infrastructure.Fontes;

/// <summary>Estado em memoria dos leitores de arquivo entre ciclos (singleton).</summary>
internal sealed class EstadoLeitoresArquivo
{
    public ConcurrentDictionary<string, AnalisadorW3C> Analisadores { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<TipoFonte, bool> PrimeiroCicloConcluido { get; } = new();
    public ConcurrentDictionary<string, bool> PastasAusentesAvisadas { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Le incrementalmente arquivos de log W3C, guardando a posicao (em bytes) de cada arquivo no banco.
/// Arquivos que ja existiam na primeira execucao comecam do fim (evita bloquear pelo historico);
/// arquivos novos (rotacao diaria) sao lidos desde o inicio. As posicoes sao salvas pelo ciclo do Worker.
/// </summary>
internal abstract class LeitorArquivosW3C(
    IRepositorioPosicoesLeitura posicoes,
    EstadoLeitoresArquivo estado,
    ResolvedorSitesIis sites,
    TimeProvider relogio,
    ILogger logger) : IFonteEventos
{
    private const int MaximoBytesPorCiclo = 32 * 1024 * 1024;
    private static readonly TimeSpan IdadeMaximaArquivo = TimeSpan.FromDays(2);

    public abstract TipoFonte Fonte { get; }

    protected abstract string PadraoArquivo { get; }

    protected abstract string ObterPasta(Configuracao configuracao);

    public abstract bool EstaHabilitada(Configuracao configuracao);

    public async Task<IReadOnlyList<EventoDetectado>> LerNovosEventosAsync(Configuracao configuracao, CancellationToken ct)
    {
        var pasta = ObterPasta(configuracao);
        var eventos = new List<EventoDetectado>();
        if (!Directory.Exists(pasta))
        {
            if (estado.PastasAusentesAvisadas.TryAdd(pasta, true))
                logger.LogWarning("Pasta de log {Pasta} nao encontrada para a fonte {Fonte}. Verificando novamente a cada ciclo", pasta, Fonte);

            // Arquivos de uma pasta que surgir depois do inicio sao novos e devem ser lidos desde o comeco.
            estado.PrimeiroCicloConcluido[Fonte] = true;
            return eventos;
        }

        if (estado.PastasAusentesAvisadas.TryRemove(pasta, out _))
            logger.LogInformation("Pasta de log {Pasta} encontrada para a fonte {Fonte}", pasta, Fonte);

        var primeiroCiclo = !estado.PrimeiroCicloConcluido.GetValueOrDefault(Fonte);
        var agora = relogio.GetUtcNow().UtcDateTime;

        var arquivos = new DirectoryInfo(pasta)
            .EnumerateFiles(PadraoArquivo, SearchOption.AllDirectories)
            .Where(a => agora - a.LastWriteTimeUtc < IdadeMaximaArquivo)
            .OrderBy(a => a.LastWriteTimeUtc);

        foreach (var arquivo in arquivos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await LerArquivoAsync(arquivo, primeiroCiclo, eventos, agora, ct);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Falha ao ler o log {Arquivo}", arquivo.FullName);
            }
        }

        estado.PrimeiroCicloConcluido[Fonte] = true;
        return eventos;
    }

    private async Task LerArquivoAsync(FileInfo arquivo, bool primeiroCiclo, List<EventoDetectado> eventos, DateTime agora, CancellationToken ct)
    {
        var chave = $"{Fonte}:{arquivo.FullName.ToLowerInvariant()}";
        var posicao = await posicoes.ObterAsync(chave, ct);
        var tamanho = arquivo.Length;

        if (posicao is null)
        {
            posicao = PosicaoLeitura.Criar(chave, primeiroCiclo ? tamanho : 0, agora);
            posicoes.Adicionar(posicao);
            if (primeiroCiclo)
                logger.LogInformation("Log {Arquivo} existente: leitura iniciada a partir do fim", arquivo.FullName);
        }
        else if (tamanho < posicao.Posicao)
        {
            logger.LogInformation("Log {Arquivo} foi truncado ou substituido, relendo desde o inicio", arquivo.FullName);
            posicao.Atualizar(0, agora);
            estado.Analisadores.TryRemove(arquivo.FullName, out _);
        }

        if (tamanho == posicao.Posicao)
            return;

        await using var fluxo = new FileStream(arquivo.FullName, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 64 * 1024, useAsync: true);

        if (!estado.Analisadores.TryGetValue(arquivo.FullName, out var analisador) && posicao.Posicao > 0)
            analisador = await LerCabecalhoAteAsync(fluxo, posicao.Posicao, ct);

        var inicio = posicao.Posicao;
        var quantidade = (int)Math.Min(tamanho - inicio, MaximoBytesPorCiclo);
        var buffer = new byte[quantidade];
        fluxo.Seek(inicio, SeekOrigin.Begin);
        await fluxo.ReadExactlyAsync(buffer, ct);

        var ultimaQuebra = Array.LastIndexOf(buffer, (byte)'\n');
        var idSite = AnalisadorW3C.ExtrairIdSite(arquivo.Directory?.Name);
        if (ultimaQuebra < 0)
            return;

        var texto = Encoding.UTF8.GetString(buffer, 0, ultimaQuebra + 1);
        foreach (var bruta in texto.Split('\n'))
        {
            var linha = bruta.TrimEnd('\r');
            if (linha.Length == 0)
                continue;

            if (AnalisadorW3C.EhDiretiva(linha))
            {
                analisador = AnalisadorW3C.DeDiretivaCampos(linha) ?? analisador;
                continue;
            }

            var evento = analisador?.Interpretar(linha, Fonte, idSite, sites.ObterNome);
            if (evento is not null)
                eventos.Add(evento);
        }

        if (analisador is not null)
            estado.Analisadores[arquivo.FullName] = analisador;
        posicao.Atualizar(inicio + ultimaQuebra + 1, agora);
    }

    /// <summary>Ao retomar a leitura no meio do arquivo (ex.: apos reiniciar o servico), recupera o ultimo "#Fields:".</summary>
    private static async Task<AnalisadorW3C?> LerCabecalhoAteAsync(FileStream fluxo, long limite, CancellationToken ct)
    {
        AnalisadorW3C? analisador = null;
        fluxo.Seek(0, SeekOrigin.Begin);
        using var leitor = new StreamReader(fluxo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 64 * 1024, leaveOpen: true);
        long lidos = 0;
        while (lidos < limite && await leitor.ReadLineAsync(ct) is { } linha)
        {
            lidos += Encoding.UTF8.GetByteCount(linha) + 2;
            if (linha.StartsWith("#Fields:", StringComparison.OrdinalIgnoreCase))
                analisador = AnalisadorW3C.DeDiretivaCampos(linha) ?? analisador;
        }

        return analisador;
    }
}

internal sealed class LeitorLogIis(IRepositorioPosicoesLeitura posicoes, EstadoLeitoresArquivo estado, ResolvedorSitesIis sites, TimeProvider relogio, ILogger<LeitorLogIis> logger)
    : LeitorArquivosW3C(posicoes, estado, sites, relogio, logger)
{
    public override TipoFonte Fonte => TipoFonte.LogIis;
    protected override string PadraoArquivo => "u_ex*.log";
    protected override string ObterPasta(Configuracao configuracao) => configuracao.CaminhoLogIis;
    public override bool EstaHabilitada(Configuracao configuracao) => configuracao.MonitorarLogIis;
}

internal sealed class LeitorHttpErr(IRepositorioPosicoesLeitura posicoes, EstadoLeitoresArquivo estado, ResolvedorSitesIis sites, TimeProvider relogio, ILogger<LeitorHttpErr> logger)
    : LeitorArquivosW3C(posicoes, estado, sites, relogio, logger)
{
    public override TipoFonte Fonte => TipoFonte.HttpErr;
    protected override string PadraoArquivo => "httperr*.log";
    protected override string ObterPasta(Configuracao configuracao) => configuracao.CaminhoHttpErr;
    public override bool EstaHabilitada(Configuracao configuracao) => configuracao.MonitorarHttpErr;
}
