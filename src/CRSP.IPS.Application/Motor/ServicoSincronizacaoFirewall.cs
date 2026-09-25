using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Motor;

/// <summary>Estado em memoria do Worker entre ciclos (evita reescrever o firewall sem necessidade).</summary>
public sealed class EstadoMotor
{
    /// <summary>Ultimo conteudo aplicado em cada conjunto de bloqueios (chave = enderecos ordenados).</summary>
    public Dictionary<ConjuntoRegrasFirewall, string> ChavesBloqueios { get; } = [];
    public string? UltimaVersaoListasExternas { get; set; }
    public string? UltimaChavePoliticaPaises { get; set; }
    public DateTime UltimaVerificacaoCompleta { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Reconcilia o firewall com o estado desejado no banco. Os bloqueios ficam em conjuntos de regras separados pela
/// fonte que os gerou (Log IIS, HTTPERR, eventos do Windows), mais bloqueios manuais, lista negra e listas externas.
/// Tambem expira bloqueios vencidos. Idempotente: pode rodar a cada 2 segundos.
/// </summary>
public sealed class ServicoSincronizacaoFirewall(
    IRepositorioBloqueios bloqueios,
    IRepositorioRegras regras,
    IRepositorioListas listas,
    IRepositorioListasExternas listasExternas,
    IRepositorioConfiguracao configuracoes,
    IRepositorioStatusWorker statusWorker,
    IServicoFirewall firewall,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    EstadoMotor estado,
    TimeProvider relogio,
    ILogger<ServicoSincronizacaoFirewall> logger)
{
    private const int EnderecosPorRegra = 1000;
    private static readonly TimeSpan IntervaloVerificacaoCompleta = TimeSpan.FromMinutes(5);

    private static readonly ConjuntoRegrasFirewall[] ConjuntosBloqueio =
    [
        ConjuntoRegrasFirewall.LogIis,
        ConjuntoRegrasFirewall.HttpErr,
        ConjuntoRegrasFirewall.EventoWindows,
        ConjuntoRegrasFirewall.Manual,
        ConjuntoRegrasFirewall.ListaNegra
    ];

    public async Task SincronizarAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var verificacaoCompleta = agora - estado.UltimaVerificacaoCompleta >= IntervaloVerificacaoCompleta;

        // Atualizacao de versao: regras com nomes antigos saem e todos os conjuntos sao reaplicados com os nomes novos.
        if (verificacaoCompleta && firewall.RemoverRegrasObsoletas() > 0)
        {
            estado.ChavesBloqueios.Clear();
            estado.UltimaVersaoListasExternas = null;
            estado.UltimaChavePoliticaPaises = null;
        }

        await SincronizarBloqueiosAsync(agora, verificacaoCompleta, ct);
        await SincronizarListasExternasAsync(verificacaoCompleta, ct);

        if (verificacaoCompleta)
            estado.UltimaVerificacaoCompleta = agora;

        await unidadeDeTrabalho.SalvarAsync(ct);
    }

    private async Task SincronizarBloqueiosAsync(DateTime agora, bool verificacaoCompleta, CancellationToken ct)
    {
        var ativos = (await bloqueios.ListarAtivosAsync(ct)).ToList();

        foreach (var vencido in ativos.Where(b => b.DeveExpirar(agora)).ToList())
        {
            vencido.Expirar(agora);
            ativos.Remove(vencido);
            logger.LogInformation("Bloqueio do IP {Ip} expirou", vencido.Ip);
        }

        var reais = ativos.Where(b => !b.Simulado).ToList();
        var fontesDasRegras = (await regras.ListarAsync(ct)).ToDictionary(r => r.Id, r => r.Fonte);
        var desejados = ConjuntosBloqueio.ToDictionary(c => c, _ => new List<FaixaIp>());

        foreach (var bloqueio in reais)
        {
            if (EnderecoIp.TentarConverter(bloqueio.Ip, out var ip))
                desejados[DefinirConjunto(bloqueio, fontesDasRegras)].Add(FaixaIp.DeEndereco(ip));
        }

        desejados[ConjuntoRegrasFirewall.ListaNegra].AddRange((await listas.ListarAsync(TipoLista.Negra, ct)).Select(e => e.ObterFaixa()));

        var alterou = false;
        var totalEnderecos = 0;
        var totalRegras = 0;
        foreach (var (conjunto, faixas) in desejados)
        {
            var desejado = FaixaIp.Mesclar(faixas);
            totalEnderecos += desejado.Count;
            totalRegras += (desejado.Count + EnderecosPorRegra - 1) / EnderecosPorRegra;

            var chave = MontarChave(desejado);
            if (!verificacaoCompleta && estado.ChavesBloqueios.TryGetValue(conjunto, out var anterior) && anterior == chave)
                continue;

            Aplicar(conjunto, desejado, chave, verificacaoCompleta);
            estado.ChavesBloqueios[conjunto] = chave;
            alterou = true;
        }

        if (alterou)
            (await statusWorker.ObterAsync(ct))?.RegistrarSincronizacao(agora, totalRegras, totalEnderecos);

        foreach (var bloqueio in reais)
            bloqueio.MarcarAplicadoNoFirewall(agora);
    }

    /// <summary>
    /// Conjunto pela fonte da regra que gerou o bloqueio (inclusive bloqueios por pais ou lista externa reativa,
    /// que guardam a regra do evento suspeito). Sem regra: bloqueio manual.
    /// </summary>
    private static ConjuntoRegrasFirewall DefinirConjunto(Bloqueio bloqueio, IReadOnlyDictionary<int, TipoFonte> fontesDasRegras) =>
        bloqueio.RegraId is { } regraId && fontesDasRegras.TryGetValue(regraId, out var fonte)
            ? fonte switch
            {
                TipoFonte.LogIis => ConjuntoRegrasFirewall.LogIis,
                TipoFonte.HttpErr => ConjuntoRegrasFirewall.HttpErr,
                _ => ConjuntoRegrasFirewall.EventoWindows
            }
            : ConjuntoRegrasFirewall.Manual;

    /// <summary>
    /// A "versao" (listas ativas + data da ultima atualizacao + modo simulacao) e barata de consultar;
    /// as entradas so sao carregadas quando ela muda ou na verificacao completa.
    /// </summary>
    private async Task SincronizarListasExternasAsync(bool verificacaoCompleta, CancellationToken ct)
    {
        var simulacao = (await configuracoes.ObterAsync(ct)).ModoSimulacao;
        var versao = $"{simulacao}|{await listasExternas.ObterVersaoAsync(ct)}";
        if (versao == estado.UltimaVersaoListasExternas && !verificacaoCompleta)
            return;

        // Somente o modo Ativa vai ao firewall; o modo Reativa bloqueia pelo motor, IP a IP, no primeiro evento suspeito.
        IReadOnlyList<FaixaIp> desejado = [];
        if (!simulacao)
        {
            var ativas = (await listasExternas.ListarAsync(ct)).Where(l => l.Modo == ModoListaExterna.Ativa).Select(l => l.Id).ToList();
            desejado = FaixaIp.Mesclar((await listasExternas.ListarEntradasAsync(ativas, ct)).Select(e => e.ObterFaixa()));
        }

        Aplicar(ConjuntoRegrasFirewall.ListasExternas, desejado, MontarChave(desejado), verificacaoCompleta);
        estado.UltimaVersaoListasExternas = versao;
    }

    /// <summary>Compara com o firewall e reescreve somente se houver diferenca.</summary>
    private void Aplicar(ConjuntoRegrasFirewall conjunto, IReadOnlyList<FaixaIp> desejado, string chave, bool verificacaoCompleta)
    {
        var atual = MontarChave(FaixaIp.Mesclar(firewall.LerEnderecos(conjunto)));
        if (atual == chave)
        {
            if (verificacaoCompleta)
                logger.LogDebug("Firewall conferido ({Conjunto}), sem divergencias", conjunto);
            return;
        }

        var regrasUsadas = firewall.SubstituirEnderecos(conjunto, desejado);
        logger.LogInformation(
            "Firewall atualizado ({Conjunto}): {Enderecos} enderecos em {Regras} regras",
            conjunto, desejado.Count, regrasUsadas);
    }

    private static string MontarChave(IEnumerable<FaixaIp> faixas) =>
        string.Join(',', faixas.Select(f => f.ParaTextoFirewall()).Order(StringComparer.Ordinal));
}
