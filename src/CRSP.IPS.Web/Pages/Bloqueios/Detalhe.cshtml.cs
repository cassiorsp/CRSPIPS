using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Bloqueios;

public sealed class DetalheModel(ServicoBloqueios servicoBloqueios) : PaginaBase
{
    [BindProperty(SupportsGet = true)]
    public string Ip { get; set; } = string.Empty;

    public DetalheIp Detalhe { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var resultado = await servicoBloqueios.ObterDetalheAsync(Ip, ct);
        if (!resultado.Sucesso || resultado.Valor is null)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage("/Bloqueios/Index");
        }

        Detalhe = resultado.Valor;
        return Page();
    }
}
