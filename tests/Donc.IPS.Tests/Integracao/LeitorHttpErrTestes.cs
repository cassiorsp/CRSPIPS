using System.Text;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Motor;
using Donc.IPS.Infrastructure.Fontes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Donc.IPS.Tests.Integracao;

public class LeitorHttpErrTestes
{
    [Fact]
    public async Task RajadaDeForbiddenNoHttpErrGeraBloqueio()
    {
        await using var ambiente = await AmbienteTeste.CriarAsync();
        var pasta = Path.Combine(Path.GetTempPath(), $"crspips-httperr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);
        var estado = new EstadoLeitoresArquivo();
        try
        {
            await ambiente.ExecutarAsync(async p =>
            {
                var configuracao = await p.GetRequiredService<IRepositorioConfiguracao>().ObterAsync();
                configuracao.AlterarMotor(true, "1h, 24h", 30, true, pasta + "-iis", true, pasta, false, 30, DateTime.UtcNow, "teste");
                await p.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync();
            });

            // Primeiro ciclo com a pasta vazia: arquivos que surgirem depois sao lidos desde o inicio.
            Assert.Empty(await LerAsync(ambiente, estado));

            var conteudo = new StringBuilder()
                .Append("#Software: Microsoft HTTP API 2.0\r\n#Version: 1.0\r\n#Date: 2026-09-23 20:15:00\r\n")
                .Append("#Fields: date time c-ip c-port s-ip s-port cs-version cs-method cs-uri streamid sc-status s-siteid s-reason s-queuename\r\n");
            for (var i = 0; i < 25; i++)
                conteudo.Append($"2026-09-23 20:15:{10 + i:D2} 8.231.218.211 5{i:D4} 10.0.0.5 443 HTTP/1.1 GET / - 403 - Forbidden - \r\n");
            conteudo.Append("2026-09-23 20:15:40 198.51.100.9 51000 10.0.0.5 443 HTTP/1.1 GET / - - - Timer_ConnectionIdle - \r\n");
            await File.WriteAllTextAsync(Path.Combine(pasta, "httperr1.log"), conteudo.ToString());

            var eventos = await LerAsync(ambiente, estado);
            Assert.Equal(26, eventos.Count);
            Assert.All(eventos.Take(25), e => Assert.Equal("Forbidden", e.MotivoHttpErr));

            var bloqueados = await ambiente.ExecutarAsync(p => p.GetRequiredService<ServicoDeteccao>().ProcessarAsync(eventos));
            Assert.Equal(1, bloqueados);
            var bloqueio = await ambiente.ExecutarAsync(p => p.GetRequiredService<IRepositorioBloqueios>().ObterAtivoPorIpAsync("8.231.218.211"));
            Assert.Equal("Requisições malformadas (HTTPERR)", bloqueio!.Motivo);

            // Uma segunda leitura nao devolve as mesmas linhas.
            Assert.Empty(await LerAsync(ambiente, estado));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    private static Task<IReadOnlyList<Application.Modelos.EventoDetectado>> LerAsync(AmbienteTeste ambiente, EstadoLeitoresArquivo estado) =>
        ambiente.ExecutarAsync(async p =>
        {
            var leitor = new LeitorHttpErr(p.GetRequiredService<IRepositorioPosicoesLeitura>(), estado, new ResolvedorSitesIis(NullLogger<ResolvedorSitesIis>.Instance, "inexistente.config"), TimeProvider.System, NullLogger<LeitorHttpErr>.Instance);
            var configuracao = await p.GetRequiredService<IRepositorioConfiguracao>().ObterAsync();
            var eventos = await leitor.LerNovosEventosAsync(configuracao, CancellationToken.None);
            await p.GetRequiredService<IUnidadeDeTrabalho>().SalvarAsync();
            return eventos;
        });
}
