using Donc.IPS.Application.Modelos;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Donc.IPS.Web.Pages.Auditoria;

public sealed class IndexModel(ServicoAuditoria servicoAuditoria) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public Pagina<RegistroAuditoria> Resultado { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken ct) => Resultado = await servicoAuditoria.PesquisarAsync(Busca, Pagina, ct);
}
