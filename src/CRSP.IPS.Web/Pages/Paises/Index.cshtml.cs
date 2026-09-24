using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Paises;

public sealed class IndexModel(
    ServicoConfiguracao servicoConfiguracao,
    IProvedorFaixasPais faixasPais,
    IRepositorioStatusWorker statusWorker) : PaginaBase
{
    [BindProperty] public ModoPoliticaPaises Modo { get; set; }
    [BindProperty] public AplicacaoPoliticaPaises Aplicacao { get; set; }
    [BindProperty] public List<string> Paises { get; set; } = [];
    [BindProperty] public string Portas { get; set; } = string.Empty;

    public bool BaseDisponivel { get; private set; }
    public bool ModoSimulacao { get; private set; }
    public string? ResumoAplicado { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var configuracao = await servicoConfiguracao.ObterAsync(ct);
        Modo = configuracao.ModoPaises;
        Aplicacao = configuracao.AplicacaoPaises;
        Paises = configuracao.ObterPaises().ToList();
        Portas = configuracao.PortasPolitica;
        await CarregarContextoAsync(configuracao, ct);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var portas = Portas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var porta) ? porta : -1)
            .ToList();
        if (portas.Any(p => p is < 1 or > 65535))
            return Concluir(Resultado.Falha("Portas inválidas. Use números separados por vírgula, ex: 3389,443."), string.Empty);

        return Concluir(
            await servicoConfiguracao.SalvarPoliticaPaisesAsync(new DadosPoliticaPaises(Modo, Aplicacao, Paises, portas)),
            "Política de países salva. O motor aplica a alteração em instantes.");
    }

    private async Task CarregarContextoAsync(Configuracao configuracao, CancellationToken ct)
    {
        BaseDisponivel = faixasPais.EstaDisponivel;
        ModoSimulacao = configuracao.ModoSimulacao;
        ResumoAplicado = (await statusWorker.ObterAsync(ct))?.ResumoPoliticaPaises;
    }
}
