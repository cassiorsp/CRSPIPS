using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Infrastructure.Firewall;
using CRSP.IPS.Infrastructure.Fontes;

namespace CRSP.IPS.Tests.Motor;

public class ComponentesMotorTestes
{
    private static readonly DateTime Base = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Contador_DescartaOcorrenciasForaDaJanela()
    {
        var contador = new ContadorJanelaDeslizante();
        var janela = TimeSpan.FromSeconds(60);

        Assert.Equal(1, contador.Registrar(1, "203.0.113.7", Base, janela));
        Assert.Equal(2, contador.Registrar(1, "203.0.113.7", Base.AddSeconds(30), janela));
        Assert.Equal(2, contador.Registrar(1, "203.0.113.7", Base.AddSeconds(75), janela));
        Assert.Equal(1, contador.Registrar(2, "203.0.113.7", Base, janela));
        Assert.Equal(1, contador.Registrar(1, "198.51.100.1", Base, janela));

        contador.Zerar(1, "203.0.113.7");
        Assert.Equal(1, contador.Registrar(1, "203.0.113.7", Base.AddSeconds(80), janela));
    }

    [Fact]
    public void LimitadorAmostras_PermiteNoMaximoVintePorHora()
    {
        var limitador = new LimitadorAmostras();
        var aceitas = Enumerable.Range(0, 30).Count(i => limitador.DeveRegistrar(1, "203.0.113.7", Base.AddSeconds(i)));

        Assert.Equal(LimitadorAmostras.MaximoPorHora, aceitas);
        Assert.True(limitador.DeveRegistrar(1, "203.0.113.7", Base.AddHours(1)));
    }

    [Fact]
    public void Avaliador_CasaCriteriosPorFonte()
    {
        var avaliador = new AvaliadorRegras();
        var regra404 = RegraDeteccao.Criar("404", null, TipoFonte.LogIis, TipoCriterio.CodigoStatus, "404, 403", 10, 60, true, Base);
        var regraUrl = RegraDeteccao.Criar("url", null, TipoFonte.LogIis, TipoCriterio.PadraoUrl, @"wp-login\.php|/\.env", 1, 60, true, Base);
        var regraRdp = RegraDeteccao.Criar("rdp", null, TipoFonte.EventoWindows, TipoCriterio.IdEventoWindows, "4625,140", 5, 300, true, Base);
        var ip = EnderecoIp.Converter("203.0.113.7");

        Assert.True(avaliador.Corresponde(regra404, new EventoDetectado(ip, TipoFonte.LogIis, Base, CodigoStatus: 403)));
        Assert.False(avaliador.Corresponde(regra404, new EventoDetectado(ip, TipoFonte.LogIis, Base, CodigoStatus: 200)));
        Assert.False(avaliador.Corresponde(regra404, new EventoDetectado(ip, TipoFonte.HttpErr, Base, CodigoStatus: 404)));
        Assert.True(avaliador.Corresponde(regraUrl, new EventoDetectado(ip, TipoFonte.LogIis, Base, Url: "/blog/WP-LOGIN.php")));
        Assert.True(avaliador.Corresponde(regraUrl, new EventoDetectado(ip, TipoFonte.LogIis, Base, Url: "/.env")));
        Assert.False(avaliador.Corresponde(regraUrl, new EventoDetectado(ip, TipoFonte.LogIis, Base, Url: "/produtos")));
        Assert.True(avaliador.Corresponde(regraRdp, new EventoDetectado(ip, TipoFonte.EventoWindows, Base, IdEventoWindows: 140)));
    }

    [Fact]
    public void Avaliador_AceitaRegexComLookaroundUsandoMotorTradicional()
    {
        var regra = RegraDeteccao.Criar("lookahead", null, TipoFonte.LogIis, TipoCriterio.PadraoUrl, @"^/admin(?!/publico)", 1, 60, true, Base);
        var avaliador = new AvaliadorRegras();
        var ip = EnderecoIp.Converter("203.0.113.7");

        Assert.True(avaliador.Corresponde(regra, new EventoDetectado(ip, TipoFonte.LogIis, Base, Url: "/admin/config")));
        Assert.False(avaliador.Corresponde(regra, new EventoDetectado(ip, TipoFonte.LogIis, Base, Url: "/admin/publico")));
    }

    [Fact]
    public void Avaliador_ResisteAUrlLongaSemAtrasar()
    {
        var regra = RegraDeteccao.Criar("sql", null, TipoFonte.LogIis, TipoCriterio.PadraoUrl,
            @"(%27|')(\+|%20|\s)*or(\+|%20|\s)+[0-9'%]+=", 1, 60, true, Base);
        var url = "/busca?q='" + string.Concat(Enumerable.Repeat("%20", 700)) + "x";
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        Assert.False(new AvaliadorRegras().Corresponde(regra, new EventoDetectado(EnderecoIp.Converter("203.0.113.7"), TipoFonte.LogIis, Base, Url: url)));
        Assert.True(cronometro.ElapsedMilliseconds < 2000);
    }

    [Fact]
    public void AnalisadorW3C_InterpretaLinhaDoIis()
    {
        var analisador = AnalisadorW3C.DeDiretivaCampos(
            "#Fields: date time s-ip cs-method cs-uri-stem cs-uri-query s-port cs-username c-ip cs(User-Agent) cs(Referer) sc-status sc-substatus sc-win32-status time-taken");
        Assert.NotNull(analisador);

        var evento = analisador.Interpretar(
            "2026-09-23 14:05:09 10.0.0.5 GET /wp-login.php a=1 443 - 203.0.113.7 Mozilla/5.0+(X11) - 404 0 2 15",
            TipoFonte.LogIis);

        Assert.NotNull(evento);
        Assert.Equal("203.0.113.7", evento.Ip.ToString());
        Assert.Equal(new DateTime(2026, 9, 23, 14, 5, 9, DateTimeKind.Utc), evento.OcorridoEmUtc);
        Assert.Equal(DateTimeKind.Utc, evento.OcorridoEmUtc.Kind);
        Assert.Equal("GET", evento.Metodo);
        Assert.Equal("/wp-login.php?a=1", evento.Url);
        Assert.Equal(404, evento.CodigoStatus);
        Assert.Equal("Mozilla/5.0 (X11)", evento.UserAgent);
    }

    [Fact]
    public void AnalisadorW3C_InterpretaLinhaDoHttpErr()
    {
        var analisador = AnalisadorW3C.DeDiretivaCampos(
            "#Fields: date time c-ip c-port s-ip s-port cs-version cs-method cs-uri streamid sc-status s-siteid s-reason s-queuename");
        Assert.NotNull(analisador);

        var evento = analisador.Interpretar(
            "2026-09-23 14:05:09 198.51.100.9 51234 10.0.0.5 80 HTTP/1.1 GET /%%%% - 400 - BadRequest -",
            TipoFonte.HttpErr);

        Assert.NotNull(evento);
        Assert.Equal("BadRequest", evento.MotivoHttpErr);
        Assert.Equal(400, evento.CodigoStatus);
        Assert.Equal("/%%%%", evento.Url);
    }

    [Fact]
    public void AnalisadorW3C_IgnoraLinhaSemIpValido()
    {
        var analisador = AnalisadorW3C.DeDiretivaCampos("#Fields: date time c-ip sc-status")!;
        Assert.Null(analisador.Interpretar("2026-09-23 14:05:09 - 404", TipoFonte.LogIis));
    }

    [Theory]
    [InlineData("3389", true)]
    [InlineData("80,443,3389", true)]
    [InlineData("3000-4000", true)]
    [InlineData("80,443", false)]
    [InlineData("*", false)]
    [InlineData("RPC", false)]
    public void Firewall_PortasContemAlguma(string portasRegra, bool esperado) =>
        Assert.Equal(esperado, ConversorEnderecosFirewall.PortasContemAlguma(portasRegra, [3389]));

    [Fact]
    public void Firewall_ConverteFormatosDevolvidosPeloWindows()
    {
        var faixas = ConversorEnderecosFirewall
            .Converter("203.0.113.7/255.255.255.255,10.0.0.1-10.0.0.9,LocalSubnet,2001:db8::1/128,*")
            .Select(f => f.ParaTextoFirewall());

        Assert.Equal(["203.0.113.7", "10.0.0.1-10.0.0.9", "2001:db8::1"], faixas);
    }
}
