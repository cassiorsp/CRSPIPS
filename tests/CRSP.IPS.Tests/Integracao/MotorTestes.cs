using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Motor;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.DependencyInjection;

namespace CRSP.IPS.Tests.Integracao;

public class MotorTestes
{
    private const string Atacante = "203.0.113.7";

    private static List<EventoDetectado> Gerar404(string ip, int quantidade, DateTime inicio) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new EventoDetectado(EnderecoIp.Converter(ip), TipoFonte.LogIis, inicio.AddSeconds(i), "GET", $"/pagina-{i}", 404))
            .ToList();

    private static Task<int> ProcessarAsync(AmbienteTeste ambiente, IReadOnlyList<EventoDetectado> eventos) =>
        ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos));

    private static Task DesligarSimulacaoAsync(AmbienteTeste ambiente) =>
        ambiente.ExecutarAsync(async p =>
        {
            var configuracao = await p.GetRequiredService<IRepositorioConfiguracao>().ObterAsync();
            configuracao.AlterarMotor(false, "1h, 24h, 7d", 30, true, "C:\\logs", true, "C:\\httperr", true, 30, DateTime.UtcNow, "teste");
            await p.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync();
        });

    [Fact]
    public async Task Deteccao_BloqueiaAoAtingirOLimiteEmModoSimulacao()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        Assert.Equal(0, await ProcessarAsync(ambiente, Gerar404(Atacante, 29, agora)));
        Assert.Equal(1, await ProcessarAsync(ambiente, Gerar404(Atacante, 1, agora.AddSeconds(30))));

        var bloqueio = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.NotNull(bloqueio);
        Assert.True(bloqueio.Simulado);
        Assert.Equal(OrigemBloqueio.Automatico, bloqueio.Origem);
        Assert.Equal("Excesso de 404", bloqueio.Motivo);
        Assert.Equal(agora.AddHours(1), bloqueio.ExpiraEm);

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());
        Assert.Empty(ambiente.Firewall.Bloqueados);
    }

    [Fact]
    public async Task Deteccao_IgnoraIpsProtegidos()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        Assert.Equal(0, await ProcessarAsync(ambiente, Gerar404("10.1.2.3", 100, agora)));
        Assert.Equal(0, await ProcessarAsync(ambiente, Gerar404("198.51.100.10", 100, agora)));
    }

    [Fact]
    public async Task Sincronizacao_AplicaBloqueiosReaisEExpiraVencidos()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await DesligarSimulacaoAsync(ambiente);
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        await ProcessarAsync(ambiente, Gerar404(Atacante, 30, agora));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());

        Assert.Equal([Atacante], ambiente.Firewall.Bloqueados.Select(f => f.ParaTextoFirewall()));
        var aplicado = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.NotNull(aplicado!.AplicadoNoFirewallEm);

        ambiente.Relogio.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());

        Assert.Empty(ambiente.Firewall.Bloqueados);
        Assert.Null(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante)));
    }

    [Fact]
    public async Task Sincronizacao_CorrigeAlteracaoManualNoFirewall()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync(Atacante, null, "teste"));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());
        Assert.Single(ambiente.Firewall.Bloqueados);

        ambiente.Firewall.Bloqueados.Clear();
        ambiente.Relogio.Advance(TimeSpan.FromMinutes(6));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());

        Assert.Equal([Atacante], ambiente.Firewall.Bloqueados.Select(f => f.ParaTextoFirewall()));
    }

    [Fact]
    public async Task Reincidencia_AumentaONivelDePunicao()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var agora = ambiente.Relogio.GetUtcNow().UtcDateTime;

        await ProcessarAsync(ambiente, Gerar404(Atacante, 30, agora));
        ambiente.Relogio.Advance(TimeSpan.FromHours(2));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoSincronizacaoFirewall>().SincronizarAsync());
        await ProcessarAsync(ambiente, Gerar404(Atacante, 30, ambiente.Relogio.GetUtcNow().UtcDateTime));

        var ativo = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.NotNull(ativo);
        Assert.Equal(2, ativo.NivelReincidencia);
        Assert.Equal(ativo.BloqueadoEm.AddHours(24), ativo.ExpiraEm);
    }

    [Fact]
    public async Task PoliticaPaisesReativa_BloqueiaPaisNaoPermitidoNoPrimeiroEvento()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.Geolocalizacao.Paises[Atacante] = "CN";
        ambiente.Geolocalizacao.Paises["192.0.2.50"] = "BR";
        ambiente.Geolocalizacao.Paises["192.0.2.60"] = "BR";

        var salvar = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoConfiguracao>().SalvarPoliticaPaisesAsync(
            new DadosPoliticaPaises(ModoPoliticaPaises.PermitirSomenteListados, AplicacaoPoliticaPaises.Reativa, ["BR"], [])));
        Assert.True(salvar.Sucesso, salvar.Erro);

        var evento = new EventoDetectado(EnderecoIp.Converter(Atacante), TipoFonte.LogIis, ambiente.Relogio.GetUtcNow().UtcDateTime, "GET", "/", 200);
        var brasileiro = evento with { Ip = EnderecoIp.Converter("192.0.2.60") };

        Assert.Equal(1, await ProcessarAsync(ambiente, [evento, brasileiro]));
        var bloqueio = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.Equal(OrigemBloqueio.Pais, bloqueio!.Origem);
    }

    [Fact]
    public async Task Localizacao_EPreenchidaRetroativamenteQuandoABaseFicaDisponivel()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ProcessarAsync(ambiente, Gerar404(Atacante, 30, ambiente.Relogio.GetUtcNow().UtcDateTime));
        var semPais = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.Null(semPais!.PaisCodigo);

        ambiente.Geolocalizacao.Paises[Atacante] = "CH";
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoCompletarLocalizacao>().ExecutarAsync());

        var completado = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante));
        Assert.Equal("CH", completado!.PaisCodigo);
        Assert.Equal(64500, completado.Asn);
        var eventos = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioEventos>().ListarPorIpAsync(Atacante, 50));
        Assert.All(eventos, e => Assert.Equal("CH", e.PaisCodigo));
    }

    [Fact]
    public async Task LimparHistorico_PreservaBloqueiosAtivosReaisQuandoSolicitado()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync("198.51.100.77", null, "real"));
        await ProcessarAsync(ambiente, Gerar404(Atacante, 30, ambiente.Relogio.GetUtcNow().UtcDateTime));

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoHistorico>().LimparAsync(manterBloqueiosAtivos: true));

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, resultado.Valor.Bloqueios);
        Assert.True(resultado.Valor.Eventos > 0);
        Assert.NotNull(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync("198.51.100.77")));
        Assert.Null(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante)));
        Assert.Empty(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioEventos>().ListarPorIpAsync(Atacante, 10)));

        var auditoria = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoAuditoria>().PesquisarAsync("Histórico limpo", 1));
        Assert.Single(auditoria.Itens);

        var tudo = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoHistorico>().LimparAsync(manterBloqueiosAtivos: false));
        Assert.Equal(1, tudo.Valor.Bloqueios);
        Assert.Empty(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ListarAtivosAsync()));
    }

    [Fact]
    public async Task PoliticaPaises_RecusaQuandoOAdministradorFicariaBloqueado()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        ambiente.Geolocalizacao.Paises["192.0.2.50"] = "BR";

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoConfiguracao>().SalvarPoliticaPaisesAsync(
            new DadosPoliticaPaises(ModoPoliticaPaises.PermitirSomenteListados, AplicacaoPoliticaPaises.Reativa, ["PT"], [])));

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task PoliticaPaisesFirewall_SemBaseDePaisesNaoAlteraOFirewall()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await DesligarSimulacaoAsync(ambiente);
        ambiente.Geolocalizacao.Paises["192.0.2.50"] = "BR";

        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoConfiguracao>().SalvarPoliticaPaisesAsync(
            new DadosPoliticaPaises(ModoPoliticaPaises.PermitirSomenteListados, AplicacaoPoliticaPaises.FirewallPorPortas, ["BR"], [3389])));
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoPoliticaPaisesFirewall>().AplicarAsync());

        Assert.Null(ambiente.Firewall.PlanoPaises);
        Assert.Empty(ambiente.Firewall.RegrasDesabilitadas);
    }

    [Fact]
    public async Task BloqueioManual_RespeitaProtecoes()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();

        var proprioIp = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync("192.0.2.50", null, null));
        var redeInterna = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync("192.168.0.20", null, null));
        var valido = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync(Atacante, TimeSpan.FromHours(6), "teste"));
        var duplicado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync(Atacante, null, null));

        Assert.False(proprioIp.Sucesso);
        Assert.False(redeInterna.Sucesso);
        Assert.True(valido.Sucesso);
        Assert.False(duplicado.Sucesso);
    }

    [Fact]
    public async Task ListaBranca_LiberaBloqueiosAtivosDaFaixa()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync(Atacante, null, null));

        var resultado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoListas>().AdicionarAsync(TipoLista.Branca, "203.0.113.0/24", "Parceiro"));

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Null(await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync(Atacante)));
    }

    [Fact]
    public async Task Pesquisa_FiltraPorFaixaCidr()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        foreach (var ip in new[] { "203.0.113.7", "203.0.113.200", "198.51.100.77" })
            await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().BloquearManualAsync(ip, null, null));

        var pagina = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoBloqueios>().PesquisarAsync(new FiltroBloqueios { Busca = "203.0.113.0/24" }));

        Assert.Equal(2, pagina.Total);
        Assert.All(pagina.Itens, b => Assert.StartsWith("203.0.113.", b.Ip));
    }

    [Fact]
    public async Task Usuarios_AutenticacaoTravaAposFalhasSeguidas()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var criar = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoUsuarios>().CriarAsync("Admin", "admin@teste.local", "SenhaForte123"));
        Assert.True(criar.Sucesso, criar.Erro);

        for (var i = 0; i < 5; i++)
            await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoUsuarios>().AutenticarAsync("admin@teste.local", "errada", "192.0.2.1"));

        var bloqueado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoUsuarios>().AutenticarAsync("admin@teste.local", "SenhaForte123", "192.0.2.1"));
        Assert.False(bloqueado.Sucesso);

        ambiente.Relogio.Advance(TimeSpan.FromMinutes(16));
        var liberado = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoUsuarios>().AutenticarAsync("ADMIN@teste.local", "SenhaForte123", "192.0.2.1"));
        Assert.True(liberado.Sucesso);
    }
}
