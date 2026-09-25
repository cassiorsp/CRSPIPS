using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class ListasExternasTestes
{
    private const string UrlSpamhausV4 = "https://www.spamhaus.org/drop/drop_v4.json";
    private const string UrlSpamhausV6 = "https://www.spamhaus.org/drop/drop_v6.json";
    private const string UrlDShield = "https://feeds.dshield.org/block.txt";

    private const string SpamhausV4 = """
        {"cidr":"1.10.16.0/20","sblid":"SBL256894","rir":"apnic"}
        {"cidr":"2.56.192.0/22","sblid":"SBL459831","rir":"ripencc"}
        {"cidr":"10.0.0.0/8","sblid":"SBL000001","rir":"teste"}
        {"type":"metadata","timestamp":1727136000,"size":3,"records":3,"copyright":"(c) Spamhaus"}
        """;
    private const string SpamhausV6 = """
        {"cidr":"2001:db8:bad::/48","sblid":"SBL999999","rir":"arin"}
        """;
    private const string DShield = """
        #   DShield.org Recommended Block List
        Start	End	Netblock	Attacks	Name	Country	email
        203.0.113.0	203.0.113.255	24	1234	Provedor Hostil	ZZ	abuse@exemplo
        198.51.100.0	198.51.100.255	24	999	Rede do servidor	ZZ	abuse@exemplo
        """;

    [Fact]
    public void Interpretador_LeOsTresFormatos()
    {
        var spamhaus = InterpretadorListaExterna.Interpretar(SpamhausV4, FormatoListaExterna.SpamhausJson);
        Assert.Equal(["1.10.16.0-1.10.31.255", "2.56.192.0-2.56.195.255", "10.0.0.0-10.255.255.255"], spamhaus.Select(e => e.Faixa.ParaTextoFirewall()));
        Assert.Equal("SBL256894", spamhaus[0].Referencia);

        var dshield = InterpretadorListaExterna.Interpretar(DShield, FormatoListaExterna.DShield);
        Assert.Equal(["203.0.113.0-203.0.113.255", "198.51.100.0-198.51.100.255"], dshield.Select(e => e.Faixa.ParaTextoFirewall()));

        var ipsum = InterpretadorListaExterna.Interpretar("# IPsum\n203.0.113.7\t9\n\nnao-e-ip\n2001:db8::1 3\n; comentario\n", FormatoListaExterna.TextoSimples);
        Assert.Equal(["203.0.113.7", "2001:db8::1"], ipsum.Select(e => e.Faixa.ParaTextoFirewall()));
    }

    [Fact]
    public async Task Atualizacao_BaixaFiltraProtecaoEAplicaForaDaSimulacao()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.BaixadorListas.Conteudos[UrlSpamhausV4] = SpamhausV4;
        ambiente.BaixadorListas.Conteudos[UrlSpamhausV6] = SpamhausV6;
        ambiente.BaixadorListas.Conteudos[UrlDShield] = DShield;

        await AtualizarAsync(ambiente);
        var spamhaus = await ObterListaAsync(ambiente, "Spamhaus DROP");

        Assert.Equal(3, spamhaus.Quantidade);
        Assert.Equal(1, spamhaus.Removidas);
        Assert.Null(spamhaus.UltimoErro);

        // Em simulacao (padrao) nada vai para o firewall.
        await SincronizarAsync(ambiente);
        Assert.Empty(ambiente.Firewall.ListasExternas);

        await DesligarSimulacaoAsync(ambiente);
        await SincronizarAsync(ambiente);
        var aplicadas = ambiente.Firewall.ListasExternas.Select(f => f.ParaTextoFirewall()).ToList();
        Assert.Contains("1.10.16.0-1.10.31.255", aplicadas);
        Assert.Contains("203.0.113.0-203.0.113.255", aplicadas);
        Assert.DoesNotContain("198.51.100.0-198.51.100.255", aplicadas);
        Assert.Equal(1, (await ObterListaAsync(ambiente, "DShield Top 20")).Removidas);
        Assert.DoesNotContain(aplicadas, f => f.StartsWith("10."));
        Assert.Empty(ambiente.Firewall.Bloqueados);
    }

    [Fact]
    public async Task Atualizacao_ComFalhaOuConteudoInvalidoMantemVersaoAnterior()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.BaixadorListas.Conteudos[UrlSpamhausV4] = SpamhausV4;
        ambiente.BaixadorListas.Conteudos[UrlSpamhausV6] = SpamhausV6;
        await AtualizarAsync(ambiente);

        ambiente.BaixadorListas.Conteudos[UrlSpamhausV4] = "<html>pagina de erro</html>";
        ambiente.BaixadorListas.Conteudos[UrlSpamhausV6] = "";
        await SolicitarAsync(ambiente, "Spamhaus DROP");
        await AtualizarAsync(ambiente);

        var aposConteudoInvalido = await ObterListaAsync(ambiente, "Spamhaus DROP");
        Assert.Equal(3, aposConteudoInvalido.Quantidade);
        Assert.NotNull(aposConteudoInvalido.UltimoErro);

        ambiente.BaixadorListas.Conteudos.Remove(UrlSpamhausV4);
        await SolicitarAsync(ambiente, "Spamhaus DROP");
        await AtualizarAsync(ambiente);

        var aposFalhaDeRede = await ObterListaAsync(ambiente, "Spamhaus DROP");
        Assert.Equal(3, aposFalhaDeRede.Quantidade);
        var entradas = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioListasExternas>().ListarEntradasAsync([aposFalhaDeRede.Id]));
        Assert.Equal(3, entradas.Count);
    }

    [Fact]
    public async Task ModoAvaliacao_RegistraCoincidenciasSemAplicarNoFirewall()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await DesligarSimulacaoAsync(ambiente);
        ambiente.BaixadorListas.Conteudos["https://cinsscore.com/list/ci-badguys.txt"] = "203.0.113.50\n";

        var cins = await ObterListaAsync(ambiente, "CINS Army");
        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().DefinirModoAsync(cins.Id, ModoListaExterna.Avaliacao));
        Assert.True(resultado.Sucesso, resultado.Erro);
        await AtualizarAsync(ambiente);

        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;
        var visitas = Enumerable.Range(0, 3)
            .Select(i => new EventoDetectado(EnderecoIp.Converter("203.0.113.50"), TipoFonte.LogIis, agora.AddSeconds(i), "GET", "/produtos", 200, Site: "loja.exemplo.com.br"))
            .ToList();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoDeteccao>().ProcessarAsync(visitas));
        await SincronizarAsync(ambiente);

        var coincidencia = Assert.Single(await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().ListarCoincidenciasAsync()));
        Assert.Equal("203.0.113.50", coincidencia.Ip);
        Assert.Equal(3, coincidencia.Quantidade);
        Assert.Equal("loja.exemplo.com.br", coincidencia.UltimoSite);
        Assert.Empty(ambiente.Firewall.ListasExternas);

        var detalhe = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().ObterDetalheAsync("203.0.113.50"));
        Assert.Contains(detalhe.Valor!.ListasExternas, l => l.Lista == "CINS Army");
    }

    [Fact]
    public async Task ListaPersonalizada_CriaBaixaAplicaEExclui()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await DesligarSimulacaoAsync(ambiente);
        const string url = "https://listas.exemplo.com/hostis.txt";
        ambiente.BaixadorListas.Conteudos[url] = "# comentario\n45.95.0.0/16\n198.51.100.10\n";

        var dados = new DadosListaExterna("Minha lista", "Parceiro de segurança", [url], FormatoListaExterna.TextoSimples, 12, 1000, ModoListaExterna.Ativa);
        var criacao = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().CriarAsync(dados));
        Assert.True(criacao.Sucesso, criacao.Erro);

        var repetida = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().CriarAsync(dados));
        Assert.False(repetida.Sucesso);

        await AtualizarAsync(ambiente);
        await SincronizarAsync(ambiente);

        var lista = await ObterListaAsync(ambiente, "Minha lista");
        Assert.True(lista.Personalizada);
        Assert.Equal(1, lista.Quantidade);
        Assert.Equal(1, lista.Removidas);
        Assert.Contains(ambiente.Firewall.ListasExternas, f => f.ParaTextoFirewall() == "45.95.0.0-45.95.255.255");

        var exclusao = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().ExcluirAsync(lista.Id));
        Assert.True(exclusao.Sucesso, exclusao.Erro);
        await SincronizarAsync(ambiente);

        Assert.DoesNotContain(ambiente.Firewall.ListasExternas, f => f.ParaTextoFirewall() == "45.95.0.0-45.95.255.255");
        Assert.DoesNotContain(await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().ListarAsync()), l => l.Nome == "Minha lista");
    }

    [Theory]
    [InlineData("", "https://listas.exemplo.com/a.txt", 24, 100)]
    [InlineData("Sem HTTPS", "http://listas.exemplo.com/a.txt", 24, 100)]
    [InlineData("Intervalo invalido", "https://listas.exemplo.com/a.txt", 0, 100)]
    [InlineData("Limite invalido", "https://listas.exemplo.com/a.txt", 24, 500_000)]
    public async Task ListaPersonalizada_ValidaOsDados(string nome, string url, int intervalo, int limite)
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().CriarAsync(
            new DadosListaExterna(nome, null, [url], FormatoListaExterna.TextoSimples, intervalo, limite, ModoListaExterna.Avaliacao)));

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task ListaDoCatalogo_NaoPodeSerExcluida()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var spamhaus = await ObterListaAsync(ambiente, "Spamhaus DROP");

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().ExcluirAsync(spamhaus.Id));

        Assert.False(resultado.Sucesso);
        Assert.False(spamhaus.Personalizada);
    }

    private static Task AtualizarAsync(AmbienteTeste ambiente) =>
        ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoAtualizacaoListasExternas>().AtualizarPendentesAsync());

    private static Task SincronizarAsync(AmbienteTeste ambiente) =>
        ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());

    private static async Task SolicitarAsync(AmbienteTeste ambiente, string nome)
    {
        var lista = await ObterListaAsync(ambiente, nome);
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListasExternas>().SolicitarAtualizacaoAsync(lista.Id));
    }

    private static Task<Domain.Entidades.ListaExterna> ObterListaAsync(AmbienteTeste ambiente, string nome) =>
        ambiente.ExecutarAsync(async p => (await p.GetRequiredService<IRepositorioListasExternas>().ListarAsync()).Single(l => l.Nome == nome));

    private static Task DesligarSimulacaoAsync(AmbienteTeste ambiente) =>
        ambiente.ExecutarAsync(async p =>
        {
            var configuracao = await p.GetRequiredService<IRepositorioConfiguracao>().ObterAsync();
            configuracao.AlterarMotor(false, "1h, 24h", 30, true, "C:\\logs", true, "C:\\httperr", true, 30, DateTime.UtcNow, "teste");
            await p.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync();
        });
}
