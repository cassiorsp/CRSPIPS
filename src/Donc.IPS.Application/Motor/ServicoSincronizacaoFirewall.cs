using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Motor;

/// <summary>Estado em memoria do Worker entre ciclos (evita reescrever o firewall sem necessidade).</summary>
public sealed class EstadoMotor
{
    public string? UltimaChaveBloqueios { get; set; }
    public string? UltimaVersaoListasExternas { get; set; }
    public string? UltimaChavePoliticaPaises { get; set; }
    public DateTime UltimaVerificacaoCompleta { get; set; } = DateTime.MinValue;
}

/// <summary>
/// Reconcilia o firewall com o estado desejado no banco, em dois conjuntos de regras independentes:
/// bloqueios (ativos nao simulados + lista negra manual) e listas externas (fora do modo simulacao).
/// Tambem expira bloqueios vencidos. Idempotente: pode rodar a cada 2 segundos.
/// </summary>
public sealed class ServicoSincronizacaoFirewall(
    IRepositorioBloqueios bloqueios,
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
    private static readonly TimeSpan IntervaloVerificacaoCompleta = TimeSpan.FromMinutes(5);

    public async Task SincronizarAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var verificacaoCompleta = agora - estado.UltimaVerificacaoCompleta >= IntervaloVerificacaoCompleta;

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
        var listaNegra = (await listas.ListarAsync(TipoLista.Negra, ct)).Select(e => e.ObterFaixa());
        var desejado = FaixaIp.Mesclar(reais
            .Select(b => EnderecoIp.TentarConverter(b.Ip, out var ip) ? FaixaIp.DeEndereco(ip) : null)
            .OfType<FaixaIp>()
            .Concat(listaNegra));

        var chave = MontarChave(desejado);
        if (chave != estado.UltimaChaveBloqueios || verificacaoCompleta)
        {
            var regrasUsadas = Aplicar(ConjuntoRegrasFirewall.Bloqueios, desejado, chave, verificacaoCompleta);
            estado.UltimaChaveBloqueios = chave;

            var status = await statusWorker.ObterAsync(ct);
            status?.RegistrarSincronizacao(agora, regrasUsadas ?? status.RegrasNoFirewall, desejado.Count);
        }

        foreach (var bloqueio in reais)
            bloqueio.MarcarAplicadoNoFirewall(agora);
    }

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

        IReadOnlyList<FaixaIp> desejado = [];
        if (!simulacao)
        {
            var ativas = (await listasExternas.ListarAsync(ct)).Where(l => l.Modo == ModoListaExterna.Ativa).Select(l => l.Id).ToList();
            desejado = FaixaIp.Mesclar((await listasExternas.ListarEntradasAsync(ativas, ct)).Select(e => e.ObterFaixa()));
        }

        Aplicar(ConjuntoRegrasFirewall.ListasExternas, desejado, MontarChave(desejado), verificacaoCompleta);
        estado.UltimaVersaoListasExternas = versao;
    }

    /// <summary>Compara com o firewall e reescreve somente se houver diferenca. Retorna as regras usadas, ou null se nada mudou.</summary>
    private int? Aplicar(ConjuntoRegrasFirewall conjunto, IReadOnlyList<FaixaIp> desejado, string chave, bool verificacaoCompleta)
    {
        var atual = MontarChave(FaixaIp.Mesclar(firewall.LerEnderecos(conjunto)));
        if (atual == chave)
        {
            if (verificacaoCompleta)
                logger.LogDebug("Firewall conferido ({Conjunto}), sem divergencias", conjunto);
            return null;
        }

        var regras = firewall.SubstituirEnderecos(conjunto, desejado);
        logger.LogInformation(
            "Firewall atualizado ({Conjunto}): {Enderecos} enderecos em {Regras} regras",
            conjunto, desejado.Count, regras);
        return regras;
    }

    private static string MontarChave(IEnumerable<FaixaIp> faixas) =>
        string.Join(',', faixas.Select(f => f.ParaTextoFirewall()).Order(StringComparer.Ordinal));
}
