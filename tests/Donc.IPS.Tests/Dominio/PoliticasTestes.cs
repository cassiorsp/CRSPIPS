using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;
using Donc.IPS.Domain.Politicas;

namespace Donc.IPS.Tests.Dominio;

public class PoliticasTestes
{
    private static readonly TimeSpan[] Tempos = [TimeSpan.FromHours(1), TimeSpan.FromHours(24), TimeSpan.FromDays(7)];

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 2, 24)]
    [InlineData(2, 3, 168)]
    [InlineData(9, 3, 168)]
    public void Progressao_SobeUmNivelPorReincidenciaEParaNoUltimo(int anteriores, int nivelEsperado, int horasEsperadas)
    {
        var (nivel, duracao) = PoliticaProgressao.Calcular(Tempos, anteriores);

        Assert.Equal(nivelEsperado, nivel);
        Assert.Equal(TimeSpan.FromHours(horasEsperadas), duracao);
    }

    [Theory]
    [InlineData("30m", 30)]
    [InlineData("2h", 120)]
    [InlineData("1d", 1440)]
    public void DuracaoTexto_ConverteUnidades(string texto, int minutos)
    {
        Assert.True(DuracaoTexto.TentarConverter(texto, out var duracao));
        Assert.Equal(TimeSpan.FromMinutes(minutos), duracao);
        Assert.Equal(texto, DuracaoTexto.Formatar(duracao));
    }

    [Theory]
    [InlineData("")]
    [InlineData("h")]
    [InlineData("0h")]
    [InlineData("-1d")]
    [InlineData("10x")]
    public void DuracaoTexto_RejeitaInvalidos(string texto) => Assert.False(DuracaoTexto.TentarConverter(texto, out _));

    [Theory]
    [InlineData(ModoPoliticaPaises.Desativada, "CN", true)]
    [InlineData(ModoPoliticaPaises.PermitirSomenteListados, "BR", true)]
    [InlineData(ModoPoliticaPaises.PermitirSomenteListados, "CN", false)]
    [InlineData(ModoPoliticaPaises.PermitirSomenteListados, null, true)]
    [InlineData(ModoPoliticaPaises.BloquearListados, "CN", true)]
    [InlineData(ModoPoliticaPaises.BloquearListados, "RU", false)]
    public void Configuracao_PaisPermitido(ModoPoliticaPaises modo, string? pais, bool esperado)
    {
        var configuracao = new Configuracao();
        var paises = modo == ModoPoliticaPaises.BloquearListados ? new[] { "RU", "KP" } : ["BR", "PT"];
        configuracao.AlterarPoliticaPaises(modo, AplicacaoPoliticaPaises.Reativa, modo == ModoPoliticaPaises.Desativada ? [] : paises, [3389], DateTime.UtcNow, "teste");

        Assert.Equal(esperado, configuracao.PaisPermitido(pais));
    }

    [Fact]
    public void Bloqueio_ExpiraELiberaSomenteQuandoAtivo()
    {
        var agora = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
        var bloqueio = Bloqueio.Criar(EnderecoIp.Converter("203.0.113.7"), OrigemBloqueio.Automatico, "Teste", agora,
            TimeSpan.FromHours(1), 1, simulado: false, "CRSPIPS");

        Assert.True(bloqueio.EstaPendenteNoFirewall);
        Assert.False(bloqueio.DeveExpirar(agora.AddMinutes(59)));
        Assert.True(bloqueio.DeveExpirar(agora.AddHours(1)));

        bloqueio.Expirar(agora.AddHours(1));
        Assert.Equal(StatusBloqueio.Expirado, bloqueio.Status);
        Assert.Throws<InvalidOperationException>(() => bloqueio.Liberar(agora, "admin", null));
    }

    [Theory]
    [InlineData(TipoFonte.LogIis, TipoCriterio.CodigoStatus, "404", null)]
    [InlineData(TipoFonte.LogIis, TipoCriterio.CodigoStatus, "40x", "Informe números separados por vírgula.")]
    [InlineData(TipoFonte.LogIis, TipoCriterio.PadraoUrl, "([a-z", "Expressão regular inválida.")]
    [InlineData(TipoFonte.EventoWindows, TipoCriterio.CodigoStatus, "404", "O critério não é compatível com a fonte selecionada.")]
    public void Regra_Validar(TipoFonte fonte, TipoCriterio criterio, string padrao, string? erroEsperado) =>
        Assert.Equal(erroEsperado, RegraDeteccao.Validar("Regra", fonte, criterio, padrao, 10, 60));
}
