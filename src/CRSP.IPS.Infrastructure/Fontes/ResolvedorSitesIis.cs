using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Infrastructure.Fontes;

/// <summary>
/// Traduz o ID de site do IIS (pasta W3SVC14 do log, campo s-siteid do HTTPERR) para o nome do site,
/// lendo o applicationHost.config. O log padrao do IIS nao registra o hostname da requisicao.
/// Recarrega quando o arquivo muda; sem acesso ao arquivo, devolve null e o motor usa "Site {id}".
/// </summary>
internal sealed class ResolvedorSitesIis(ILogger<ResolvedorSitesIis> logger, string? caminhoConfiguracao = null)
{
    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromMinutes(5);

    private readonly string _caminho = caminhoConfiguracao ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "config", "applicationHost.config");
    private readonly Lock _trava = new();
    private IReadOnlyDictionary<string, string> _sites = new Dictionary<string, string>();
    private DateTime _versao = DateTime.MinValue;
    private DateTime _ultimaVerificacao = DateTime.MinValue;

    public string? ObterNome(string idSite)
    {
        Recarregar();
        return _sites.GetValueOrDefault(idSite);
    }

    private void Recarregar()
    {
        if (DateTime.UtcNow - _ultimaVerificacao < IntervaloVerificacao)
            return;

        lock (_trava)
        {
            if (DateTime.UtcNow - _ultimaVerificacao < IntervaloVerificacao)
                return;
            _ultimaVerificacao = DateTime.UtcNow;

            try
            {
                if (!File.Exists(_caminho))
                    return;
                var versao = File.GetLastWriteTimeUtc(_caminho);
                if (versao == _versao)
                    return;

                using var fluxo = new FileStream(_caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                _sites = XDocument.Load(fluxo)
                    .Descendants("site")
                    .Where(s => s.Parent?.Name.LocalName == "sites")
                    .Select(s => (Id: (string?)s.Attribute("id"), Nome: (string?)s.Attribute("name")))
                    .Where(s => !string.IsNullOrEmpty(s.Id) && !string.IsNullOrEmpty(s.Nome))
                    .GroupBy(s => s.Id!)
                    .ToDictionary(g => g.Key, g => g.First().Nome!);
                _versao = versao;
                logger.LogDebug("Sites do IIS carregados: {Quantidade}", _sites.Count);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                logger.LogWarning(ex, "Nao foi possivel ler os sites do IIS em {Caminho}", _caminho);
            }
        }
    }
}
