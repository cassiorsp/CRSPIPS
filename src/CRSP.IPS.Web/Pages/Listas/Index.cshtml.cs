using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Listas;

public sealed class IndexModel(ServicoListas servicoListas) : PaginaBase
{
    [BindProperty(SupportsGet = true)]
    public TipoLista Tipo { get; set; } = TipoLista.Branca;

    public IReadOnlyList<EntradaLista> Entradas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) => Entradas = await servicoListas.ListarAsync(Tipo, ct);

    public async Task<IActionResult> OnPostAdicionarAsync(string faixa, string? descricao)
    {
        var resultado = await servicoListas.AdicionarAsync(Tipo, faixa, descricao);
        return Concluir(resultado, Tipo == TipoLista.Branca ? "Adicionado à lista branca." : "Adicionado à lista negra.",
            Url.Page("/Listas/Index", new { Tipo }));
    }

    public async Task<IActionResult> OnPostRemoverAsync(int id) =>
        Concluir(await servicoListas.RemoverAsync(id), "Registro removido.", Url.Page("/Listas/Index", new { Tipo }));
}
