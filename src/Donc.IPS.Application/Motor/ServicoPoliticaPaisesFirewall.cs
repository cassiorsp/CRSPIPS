using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Motor;

/// <summary>
/// Aplica a politica de paises no modo FirewallPorPortas.
/// Permitir somente listados: cria regras de PERMISSAO nas portas para as faixas dos paises + enderecos protegidos
/// e desativa as regras de permissao de terceiros nessas portas (o Windows Firewall bloqueia por padrao o que nao e permitido).
/// Bloquear listados: cria regras de BLOQUEIO nas portas para as faixas dos paises.
/// Em modo simulacao nada e aplicado; apenas o resumo e registrado.
/// </summary>
public sealed class ServicoPoliticaPaisesFirewall(
    IRepositorioConfiguracao configuracoes,
    IRepositorioStatusWorker statusWorker,
    IRepositorioRegrasFirewallDesativadas regrasDesativadas,
    IProvedorFaixasPais faixasPais,
    IServicoFirewall firewall,
    ServicoProtecao protecao,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    EstadoMotor estado,
    TimeProvider relogio,
    ILogger<ServicoPoliticaPaisesFirewall> logger)
{
    public async Task AplicarAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        var conjuntoProtecao = await protecao.ObterAsync(ct);
        var sempreLiberadas = conjuntoProtecao.Todas;

        var chave = string.Join('|',
            configuracao.ModoPaises, configuracao.AplicacaoPaises, configuracao.PaisesPolitica, configuracao.PortasPolitica,
            configuracao.ModoSimulacao, string.Join(',', sempreLiberadas.Select(f => f.ParaTextoFirewall())), faixasPais.EstaDisponivel);
        if (chave == estado.UltimaChavePoliticaPaises)
            return;

        var status = await statusWorker.ObterAsync(ct);
        var agora = relogio.GetUtcNow().UtcDateTime;
        var usaFirewall = configuracao.ModoPaises != ModoPoliticaPaises.Desativada &&
                          configuracao.AplicacaoPaises == AplicacaoPoliticaPaises.FirewallPorPortas;

        if (!usaFirewall)
        {
            await RemoverAsync(ct);
            status?.RegistrarPoliticaPaises(null);
        }
        else if (!faixasPais.EstaDisponivel)
        {
            status?.RegistrarPoliticaPaises("Base de faixas por país (GeoLite2-Country CSV) não encontrada. Política não aplicada.");
            logger.LogWarning("Politica de paises configurada, mas a base GeoLite2-Country CSV nao foi encontrada");
        }
        else
        {
            var paises = configuracao.ObterPaises();
            var portas = configuracao.ObterPortas();
            var faixas = faixasPais.ObterFaixas(paises);
            var resumo = $"{configuracao.ModoPaises} | {string.Join(',', paises)} | portas {string.Join(',', portas)} | {faixas.Count} faixas";

            if (configuracao.ModoSimulacao)
            {
                await RemoverAsync(ct);
                status?.RegistrarPoliticaPaises($"Simulação: {resumo}");
                logger.LogInformation("Politica de paises em simulacao: {Resumo}", resumo);
            }
            else
            {
                firewall.AplicarRestricaoPaises(new PlanoRestricaoPaises(configuracao.ModoPaises, portas, faixas, sempreLiberadas));

                if (configuracao.ModoPaises == ModoPoliticaPaises.PermitirSomenteListados)
                    await DesativarRegrasConflitantesAsync(portas, agora, ct);
                else
                    await ReativarRegrasDesativadasAsync(ct);

                status?.RegistrarPoliticaPaises(resumo);
                logger.LogInformation("Politica de paises aplicada no firewall: {Resumo}", resumo);
            }
        }

        await unidadeDeTrabalho.SalvarAsync(ct);
        estado.UltimaChavePoliticaPaises = chave;
    }

    private async Task RemoverAsync(CancellationToken ct)
    {
        firewall.RemoverRestricaoPaises();
        await ReativarRegrasDesativadasAsync(ct);
    }

    private async Task DesativarRegrasConflitantesAsync(IReadOnlyList<int> portas, DateTime agora, CancellationToken ct)
    {
        var jaDesativadas = (await regrasDesativadas.ListarAsync(ct)).Select(r => r.Nome).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var nome in firewall.ListarRegrasPermissivasNasPortas(portas))
        {
            firewall.DefinirRegraHabilitada(nome, false);
            if (jaDesativadas.Add(nome))
                regrasDesativadas.Adicionar(RegraFirewallDesativada.Criar(nome, agora));
            logger.LogInformation("Regra de firewall {Regra} desativada pela politica de paises", nome);
        }
    }

    private async Task ReativarRegrasDesativadasAsync(CancellationToken ct)
    {
        foreach (var regra in await regrasDesativadas.ListarAsync(ct))
        {
            firewall.DefinirRegraHabilitada(regra.Nome, true);
            regrasDesativadas.Remover(regra);
            logger.LogInformation("Regra de firewall {Regra} reativada", regra.Nome);
        }
    }
}
