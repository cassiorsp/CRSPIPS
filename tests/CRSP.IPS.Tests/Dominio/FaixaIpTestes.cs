using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Tests.Dominio;

public class FaixaIpTestes
{
    [Theory]
    [InlineData("10.0.0.0/8", "10.0.0.0", "10.255.255.255")]
    [InlineData("192.168.1.77/24", "192.168.1.0", "192.168.1.255")]
    [InlineData("203.0.113.7", "203.0.113.7", "203.0.113.7")]
    [InlineData("203.0.113.7/255.255.255.255", "203.0.113.7", "203.0.113.7")]
    [InlineData("10.0.0.0/255.255.0.0", "10.0.0.0", "10.0.255.255")]
    [InlineData("10.0.0.50-10.0.0.1", "10.0.0.1", "10.0.0.50")]
    [InlineData("0.0.0.0/0", "0.0.0.0", "255.255.255.255")]
    [InlineData("2001:db8::/32", "2001:db8::", "2001:db8:ffff:ffff:ffff:ffff:ffff:ffff")]
    public void Converter_AceitaFormatosSuportados(string texto, string inicio, string fim)
    {
        var faixa = FaixaIp.Converter(texto);

        Assert.Equal(inicio, faixa.Inicio.ToString());
        Assert.Equal(fim, faixa.Fim.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.1-2001:db8::1")]
    [InlineData("10.0.0.0/255.0.255.0")]
    public void TentarConverter_RejeitaFormatosInvalidos(string texto) =>
        Assert.False(FaixaIp.TentarConverter(texto, out _));

    [Fact]
    public void Contem_RespeitaLimitesEFamilia()
    {
        var faixa = FaixaIp.Converter("172.16.0.0/12");

        Assert.True(faixa.Contem(EnderecoIp.Converter("172.16.0.0")));
        Assert.True(faixa.Contem(EnderecoIp.Converter("172.31.255.255")));
        Assert.False(faixa.Contem(EnderecoIp.Converter("172.32.0.0")));
        Assert.False(faixa.Contem(EnderecoIp.Converter("::ffff:ac20:0")));
        Assert.True(faixa.Contem(EnderecoIp.Converter("::ffff:172.20.1.1")));
        Assert.False(faixa.Contem(EnderecoIp.Converter("2001:db8::1")));
    }

    [Fact]
    public void Mesclar_UneFaixasAdjacentesESobrepostas()
    {
        var mescladas = FaixaIp.Mesclar(
        [
            FaixaIp.Converter("10.0.0.5"),
            FaixaIp.Converter("10.0.0.0/30"),
            FaixaIp.Converter("10.0.0.4"),
            FaixaIp.Converter("10.0.1.0/24"),
            FaixaIp.Converter("10.0.0.10"),
            FaixaIp.Converter("2001:db8::1")
        ]);

        Assert.Equal(
            ["10.0.0.0-10.0.0.5", "10.0.0.10", "10.0.1.0-10.0.1.255", "2001:db8::1"],
            mescladas.Select(f => f.ParaTextoFirewall()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EnderecoIp_NormalizaIPv4MapeadoEOrdenaNumericamente()
    {
        var mapeado = EnderecoIp.Converter("::ffff:10.0.0.1");
        Assert.True(mapeado.EhIPv4);
        Assert.Equal("10.0.0.1", mapeado.ToString());

        var menor = EnderecoIp.Converter("9.255.255.255");
        var maior = EnderecoIp.Converter("10.0.0.0");
        Assert.True(string.CompareOrdinal(menor.ObterChaveOrdenavel(), maior.ObterChaveOrdenavel()) < 0);
    }
}
