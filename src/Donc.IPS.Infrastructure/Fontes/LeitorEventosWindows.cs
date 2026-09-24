using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Infrastructure.Fontes;

/// <summary>
/// Le falhas de autenticacao do Event Log do Windows:
/// 4625 (Security) - falha de logon (RDP com NLA, SMB, etc.);
/// 140 (RdpCoreTS/Operational) - falha de credencial no RDP com o IP de origem;
/// 18456 (Application) - falha de login no SQL Server.
/// A posicao e o ultimo EventRecordID lido de cada canal.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class LeitorEventosWindows(
    IRepositorioPosicoesLeitura posicoes,
    TimeProvider relogio,
    ILogger<LeitorEventosWindows> logger) : IFonteEventos
{
    private const int MaximoPorCanal = 5000;

    private static readonly FonteCanal[] Canais =
    [
        new("Security", 4625, ExtrairPorNome("IpAddress", "TargetUserName")),
        new("Microsoft-Windows-RemoteDesktopServices-RdpCoreTS/Operational", 140, ExtrairPorNome("IPString", null)),
        new("Application", 18456, ExtrairSqlServer)
    ];

    public TipoFonte Fonte => TipoFonte.EventoWindows;

    public bool EstaHabilitada(Configuracao configuracao) => configuracao.MonitorarEventosWindows;

    public async Task<IReadOnlyList<EventoDetectado>> LerNovosEventosAsync(Configuracao configuracao, CancellationToken ct)
    {
        var eventos = new List<EventoDetectado>();
        var agora = relogio.GetUtcNow().UtcDateTime;

        foreach (var canal in Canais)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await LerCanalAsync(canal, eventos, agora, ct);
            }
            catch (EventLogNotFoundException)
            {
                logger.LogDebug("Canal {Canal} nao existe neste servidor", canal.Nome);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogWarning("Sem permissao para ler o canal {Canal}. O Worker precisa rodar como LocalSystem ou administrador", canal.Nome);
            }
            catch (EventLogException ex)
            {
                logger.LogWarning(ex, "Falha ao ler o canal {Canal}", canal.Nome);
            }
        }

        return eventos;
    }

    private async Task LerCanalAsync(FonteCanal canal, List<EventoDetectado> eventos, DateTime agora, CancellationToken ct)
    {
        var chave = $"{Fonte}:{canal.Nome}:{canal.IdEvento}";
        var posicao = await posicoes.ObterAsync(chave, ct);
        if (posicao is null)
        {
            posicao = PosicaoLeitura.Criar(chave, ObterUltimoRegistro(canal), agora);
            posicoes.Adicionar(posicao);
            return;
        }

        var consulta = new EventLogQuery(canal.Nome, PathType.LogName,
            $"*[System[(EventID={canal.IdEvento}) and (EventRecordID > {posicao.Posicao})]]");
        using var leitor = new EventLogReader(consulta);

        var ultimo = posicao.Posicao;
        var lidos = 0;
        while (lidos < MaximoPorCanal && leitor.ReadEvent() is { } registro)
        {
            using (registro)
            {
                lidos++;
                ultimo = Math.Max(ultimo, registro.RecordId ?? ultimo);
                var (ipTexto, usuario) = canal.Extrair(registro);
                if (!EnderecoIp.TentarConverter(ipTexto, out var ip))
                    continue;

                eventos.Add(new EventoDetectado(
                    ip,
                    Fonte,
                    (registro.TimeCreated ?? agora).ToUniversalTime(),
                    IdEventoWindows: canal.IdEvento,
                    Detalhe: usuario is null ? canal.Nome : $"{canal.Nome} | {usuario}"));
            }
        }

        posicao.Atualizar(ultimo, agora);
    }

    private static long ObterUltimoRegistro(FonteCanal canal)
    {
        var consulta = new EventLogQuery(canal.Nome, PathType.LogName, $"*[System[(EventID={canal.IdEvento})]]")
        {
            ReverseDirection = true
        };
        using var leitor = new EventLogReader(consulta);
        using var registro = leitor.ReadEvent();
        return registro?.RecordId ?? 0;
    }

    private static Func<EventRecord, (string? Ip, string? Usuario)> ExtrairPorNome(string campoIp, string? campoUsuario) => registro =>
    {
        var dados = XDocument.Parse(registro.ToXml())
            .Descendants()
            .Where(e => e.Name.LocalName == "Data")
            .ToDictionary(e => (string?)e.Attribute("Name") ?? string.Empty, e => e.Value, StringComparer.OrdinalIgnoreCase);
        return (dados.GetValueOrDefault(campoIp), campoUsuario is null ? null : dados.GetValueOrDefault(campoUsuario));
    };

    private static (string? Ip, string? Usuario) ExtrairSqlServer(EventRecord registro)
    {
        foreach (var propriedade in registro.Properties)
        {
            if (propriedade.Value is string texto && ExpressaoClienteSql().Match(texto) is { Success: true } resultado)
                return (resultado.Groups[1].Value, registro.Properties.FirstOrDefault()?.Value as string);
        }

        return (null, null);
    }

    [GeneratedRegex(@"CLIENT:\s*([0-9a-fA-F:.]+)")]
    private static partial Regex ExpressaoClienteSql();

    private sealed record FonteCanal(string Nome, int IdEvento, Func<EventRecord, (string? Ip, string? Usuario)> Extrair);
}
