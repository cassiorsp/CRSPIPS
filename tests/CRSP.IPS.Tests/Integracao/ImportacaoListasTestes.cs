using System.Text;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Web.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class ImportacaoListasTestes
{
    [Fact]
    public async Task Csv_AceitaPontoEVirgulaAspasComentariosECabecalho()
    {
        const string conteudo = "﻿faixa;descricao\r\n# comentario\r\n203.0.113.7;\"Scanner; porta 22\"\r\n\r\n198.51.100.0/24\r\n";

        var linhas = await LerAsync(conteudo);

        Assert.Equal(2, linhas.Count);
        Assert.Equal(new LinhaImportacao(3, "203.0.113.7", "Scanner; porta 22"), linhas[0]);
        Assert.Equal(new LinhaImportacao(5, "198.51.100.0/24", null), linhas[1]);
    }

    [Fact]
    public async Task Csv_AceitaVirgulaComoSeparador()
    {
        var linhas = await LerAsync("ip,descricao\n192.0.2.1-192.0.2.9,Faixa de teste\n");

        Assert.Equal(new LinhaImportacao(2, "192.0.2.1-192.0.2.9", "Faixa de teste"), Assert.Single(linhas));
    }

    [Fact]
    public async Task ModeloCsv_EhLidoSemErros()
    {
        var linhas = await LerAsync(ArquivoCsvListas.GerarModelo());

        Assert.Equal(4, linhas.Count);
        Assert.Contains(linhas, l => l.Faixa == "2001:db8::/32");
    }

    [Fact]
    public async Task Importar_ListaNegra_AdicionaValidasERelataErrosEDuplicadas()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>().AdicionarAsync(TipoLista.Negra, "203.0.113.0/24", "Já cadastrada"));

        var linhas = new List<LinhaImportacao>
        {
            new(2, "198.18.0.0/15", "Rede hostil"),
            new(3, "198.18.0.0/15", "Repetida no arquivo"),
            new(4, "203.0.113.0/24", null),
            new(5, "isto-nao-e-ip", null),
            new(6, "10.1.0.0/16", "Sobrepõe a rede privada da lista branca"),
            new(7, "192.0.2.50", "IP do próprio administrador"),
            new(8, "2001:db8:bad::/48", null)
        };

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>().ImportarAsync(TipoLista.Negra, linhas));

        Assert.True(resultado.Sucesso);
        var importacao = resultado.Valor!;
        Assert.Equal(2, importacao.Adicionadas);
        Assert.Equal(2, importacao.Duplicadas);
        Assert.Equal([5, 6, 7], importacao.Erros.Select(e => e.Linha));

        var negra = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>().ListarAsync(TipoLista.Negra));
        Assert.Equal(["198.18.0.0/15", "2001:db8:bad::/48", "203.0.113.0/24"], negra.Select(e => e.Faixa).Order());
    }

    [Fact]
    public async Task Importar_ListaBranca_LiberaBloqueiosAtivosDasFaixas()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync("203.0.113.7", TimeSpan.FromHours(1), null));

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>()
            .ImportarAsync(TipoLista.Branca, [new LinhaImportacao(2, "203.0.113.0/24", "Parceiro")]));

        Assert.Equal(1, resultado.Valor!.Adicionadas);
        var bloqueio = await ambiente.ExecutarAsync(p => p.GetRequiredService<Application.Abstracoes.IRepositorioBloqueios>().ObterAtivoPorIpAsync("203.0.113.7"));
        Assert.Null(bloqueio);
    }

    [Fact]
    public async Task Importar_ArquivoVazioOuGrandeDemaisEhRecusado()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var servico = (Func<IReadOnlyList<LinhaImportacao>, Task<Resultado<ResultadoImportacao>>>)(linhas =>
            ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>().ImportarAsync(TipoLista.Negra, linhas)));

        Assert.False((await servico([])).Sucesso);
        var excesso = Enumerable.Range(1, ServicoListas.LimiteLinhasImportacao + 1).Select(i => new LinhaImportacao(i, "203.0.113.7", null)).ToList();
        Assert.False((await servico(excesso)).Sucesso);
    }

    private static Task<IReadOnlyList<LinhaImportacao>> LerAsync(string conteudo) =>
        ArquivoCsvListas.LerAsync(new MemoryStream(Encoding.UTF8.GetBytes(conteudo)));
}
