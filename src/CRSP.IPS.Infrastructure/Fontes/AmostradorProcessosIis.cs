using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Infrastructure.Fontes;

/// <summary>
/// Le os processos w3wp.exe: o application pool vem do argumento "-ap" da linha de comando (WMI) e a memoria
/// privada e a CPU, do proprio processo. O Worker roda como LocalSystem, entao enxerga todos os pools.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class AmostradorProcessosIis(ResolvedorSitesIis sites, ILogger<AmostradorProcessosIis> logger) : IAmostradorProcessosIis
{
    private readonly Dictionary<int, (TimeSpan Cpu, long Instante)> _anteriores = [];
    private bool _wmiIndisponivelAvisado;

    [GeneratedRegex(@"-ap\s+""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PoolNaLinhaDeComando();

    public IReadOnlyList<SitePool> ListarSites() => sites.ListarSitesComPool();

    public IReadOnlyList<AmostraProcessoIis> Amostrar()
    {
        var amostras = new List<AmostraProcessoIis>();
        var vivos = new HashSet<int>();

        foreach (var (processoId, pool) in LerPoolsPorProcesso())
        {
            try
            {
                using var processo = Process.GetProcessById(processoId);
                processo.Refresh();
                vivos.Add(processoId);
                amostras.Add(new AmostraProcessoIis(pool, processoId, processo.PrivateMemorySize64, CalcularCpu(processo)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // O processo terminou (reciclagem do pool) entre a consulta e a leitura.
                logger.LogDebug(ex, "Processo w3wp {Pid} nao esta mais disponivel", processoId);
            }
        }

        foreach (var antigo in _anteriores.Keys.Where(pid => !vivos.Contains(pid)).ToList())
            _anteriores.Remove(antigo);

        return amostras;
    }

    private double CalcularCpu(Process processo)
    {
        var agora = Stopwatch.GetTimestamp();
        var cpu = processo.TotalProcessorTime;
        var percentual = 0d;
        if (_anteriores.TryGetValue(processo.Id, out var anterior))
        {
            var decorrido = Stopwatch.GetElapsedTime(anterior.Instante, agora);
            if (decorrido > TimeSpan.Zero)
                percentual = (cpu - anterior.Cpu).TotalMilliseconds / (decorrido.TotalMilliseconds * Environment.ProcessorCount) * 100;
        }

        _anteriores[processo.Id] = (cpu, agora);
        return Math.Clamp(percentual, 0, 100);
    }

    private Dictionary<int, string> LerPoolsPorProcesso()
    {
        var pools = new Dictionary<int, string>();
        try
        {
            using var consulta = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'w3wp.exe'");
            foreach (ManagementBaseObject processo in consulta.Get())
            {
                var comando = processo["CommandLine"] as string;
                var pool = comando is null ? null : PoolNaLinhaDeComando().Match(comando) is { Success: true } m ? m.Groups[1].Value : null;
                var pid = Convert.ToInt32(processo["ProcessId"]);
                pools[pid] = pool ?? $"w3wp-{pid}";
                processo.Dispose();
            }
        }
        catch (Exception ex) when (ex is ManagementException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            if (!_wmiIndisponivelAvisado)
            {
                logger.LogWarning(ex, "WMI indisponivel: os processos w3wp serao agrupados sem o nome do application pool");
                _wmiIndisponivelAvisado = true;
            }

            foreach (var processo in Process.GetProcessesByName("w3wp"))
            {
                pools[processo.Id] = $"w3wp-{processo.Id}";
                processo.Dispose();
            }
        }

        return pools;
    }
}
