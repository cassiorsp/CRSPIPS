using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;

namespace Donc.IPS.Infrastructure.Persistencia;

/// <summary>
/// Regras de deteccao entregues com o CRSPIPS, agrupadas por versao. Ao iniciar, as versoes ainda nao aplicadas
/// ao banco inserem suas regras (se nao houver outra com o mesmo nome). Para distribuir novas regras,
/// adicione-as com a proxima versao e incremente <see cref="VersaoAtual"/>.
/// </summary>
internal static class CatalogoRegrasPadrao
{
    public const int VersaoAtual = 4;

    public static IEnumerable<(int Versao, RegraDeteccao Regra)> Listar(DateTime agora)
    {
        // Versao 1: instalacao inicial.
        yield return (1, RegraDeteccao.Criar("Excesso de 404",
            "Muitas páginas inexistentes em pouco tempo: varredura de diretórios.",
            TipoFonte.LogIis, TipoCriterio.CodigoStatus, "404", 30, 60, true, agora));

        yield return (1, RegraDeteccao.Criar("Varredura de URLs sensíveis",
            "Tentativas de acesso a caminhos típicos de ataque (WordPress, .env, .git, phpMyAdmin).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(wp-login\.php|xmlrpc\.php|/wp-admin|/wp-content|/\.env|/\.git/|phpmyadmin|/cgi-bin/|/vendor/phpunit|/boaform|/HNAP1|/actuator|/\.aws/|/config\.json|/server-status|\.\./\.\./)",
            3, 300, true, agora));

        yield return (1, RegraDeteccao.Criar("Excesso de 401/403",
            "Muitas negações de acesso. Desativada por padrão: autenticação Windows gera 401 legítimos.",
            TipoFonte.LogIis, TipoCriterio.CodigoStatus, "401,403", 50, 60, false, agora));

        yield return (1, RegraDeteccao.Criar("Requisições malformadas (HTTPERR)",
            "Requisições rejeitadas pelo HTTP.sys antes de chegar ao IIS.",
            TipoFonte.HttpErr, TipoCriterio.MotivoHttpErr,
            "^(BadRequest|Hostname|Verb|URL|Header|FieldLength|RequestLength|Forbidden)$", 20, 60, true, agora));

        yield return (1, RegraDeteccao.Criar("Falha de login RDP / Windows",
            "Eventos 4625 (Security) e 140 (RdpCoreTS): força bruta em Área de Trabalho Remota e SMB.",
            TipoFonte.EventoWindows, TipoCriterio.IdEventoWindows, "4625,140", 5, 300, true, agora));

        yield return (1, RegraDeteccao.Criar("Falha de login SQL Server",
            "Evento 18456 do SQL Server com o IP do cliente.",
            TipoFonte.EventoWindows, TipoCriterio.IdEventoWindows, "18456", 5, 300, true, agora));

        // Versao 2: ataques e varreduras por padrao de URL.
        yield return (2, RegraDeteccao.Criar("Scanner de PHP/JSP",
            "Pedidos de páginas PHP, JSP ou CGI em servidor IIS/.NET: só scanners fazem isso. Desative se hospedar algum site PHP.",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"\.(php[0-9]?|phtml|jsp|jspx|cgi|pl)(\?|/|$)",
            3, 300, true, agora));

        yield return (2, RegraDeteccao.Criar("Injeção SQL na URL",
            "Tentativas de SQL injection (UNION SELECT, SLEEP, WAITFOR DELAY, xp_cmdshell, OR 1=1).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(union(\+|%20|\s)+(all(\+|%20|\s)+)?select|information_schema|sleep\(|sleep%28|benchmark\(|waitfor(\+|%20|\s)+delay|xp_cmdshell|(%27|')(\+|%20|\s)*or(\+|%20|\s)+[0-9'%]+=)",
            2, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("Path traversal",
            "Tentativas de sair da pasta do site e ler arquivos do sistema (../, /etc/passwd, win.ini).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(\.\.(/|\\|%2f|%5c)|%2e%2e(/|%2f|%5c)|/etc/passwd|win\.ini|boot\.ini|/proc/self/)",
            1, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("Execução remota / Log4Shell",
            "Tentativas de executar comandos no servidor (Log4Shell, cmd.exe, /bin/sh, webshells).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(\$\{jndi:|%24%7bjndi|cmd\.exe|/bin/(ba)?sh|wget(\+|%20)http|curl(\+|%20)http|c99\.php|r57\.php|base64_decode|eval\()",
            1, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("XSS na URL",
            "Tentativas de injetar script na página (cross-site scripting).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(<script|%3cscript|javascript:|%3csvg|onerror(=|%3d)|onload(=|%3d)|alert(\(|%28))",
            2, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("Arquivos sensíveis e backups",
            "Busca por configurações, chaves e backups expostos (web.config, appsettings.json, id_rsa, dump.sql).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"/(web\.config|appsettings(\.[a-z]+)?\.json|\.htaccess|\.htpasswd|\.ds_store|id_rsa|\.ssh/|\.svn/|\.hg/|\.idea/|\.vscode/|docker-compose\.ya?ml|dump\.sql|database\.sql|backup\.(zip|sql|rar|tar|gz))",
            2, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("Painéis e serviços de terceiros",
            "Varredura de Tomcat, Jenkins, Solr, Laravel e VPNs (Fortinet, Pulse, GlobalProtect).",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(/manager/html|/jenkins|/solr/|/geoserver|/nacos|/druid/|/_ignition|/telescope|/GponForm|/setup\.cgi|/api/jsonws|/struts|/weblogic|/remote/fgt_lang|/dana-na/|/global-protect)",
            2, 600, true, agora));

        yield return (2, RegraDeteccao.Criar("Varredura de Exchange/OWA",
            "Tentativas contra Exchange (ProxyShell, OWA, ECP). Desative se este servidor hospedar Exchange.",
            TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(/autodiscover/autodiscover\.json|/owa/|/ecp/|/mapi/|/ews/|/rpc/rpcproxy\.dll)",
            3, 600, true, agora));

        // Versao 3: forca bruta lenta/distribuida no RDP.
        yield return (3, RegraDeteccao.Criar("Falha de login RDP (lenta)",
            "Complementa a regra de RDP: pega quem tenta devagar para ficar abaixo de 5 falhas em 5 minutos.",
            TipoFonte.EventoWindows, TipoCriterio.IdEventoWindows, "4625,140", 10, 3600, true, agora));

        // Versao 4: scanners que acessam o servidor pelo IP ou por hostnames que nao existem no IIS.
        yield return (4, RegraDeteccao.Criar("Host inexistente (HTTPERR)",
            "Muitos pedidos para hostnames que não existem neste IIS (ou direto para o IP do servidor): varredura de IPs.",
            TipoFonte.HttpErr, TipoCriterio.MotivoHttpErr, "^NotFound$", 50, 300, true, agora));
    }
}
