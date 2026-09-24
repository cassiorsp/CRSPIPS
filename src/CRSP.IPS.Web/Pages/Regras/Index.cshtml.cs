using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Regras;

public sealed class IndexModel(ServicoRegras servicoRegras) : PaginaBase
{
    public IReadOnlyList<RegraDeteccao> Regras { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Regras = await servicoRegras.ListarAsync(ct);

    public async Task<IActionResult> OnPostAlternarAsync(int id) =>
        Concluir(await servicoRegras.AlternarAtivaAsync(id), "Regra atualizada.");

    public async Task<IActionResult> OnPostExcluirAsync(int id) =>
        Concluir(await servicoRegras.ExcluirAsync(id), "Regra excluída.");
}
