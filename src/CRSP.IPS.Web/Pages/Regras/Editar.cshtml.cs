using System.ComponentModel.DataAnnotations;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Regras;

public sealed class EditarModel(ServicoRegras servicoRegras) : PaginaBase
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public DadosFormulario Dados { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id is null)
            return Page();

        var regra = await servicoRegras.ObterAsync(Id.Value, ct);
        if (regra is null)
            return NotFound();

        Dados = new DadosFormulario
        {
            Nome = regra.Nome,
            Descricao = regra.Descricao,
            Fonte = regra.Fonte,
            Criterio = regra.Criterio,
            Padrao = regra.Padrao,
            LimiteOcorrencias = regra.LimiteOcorrencias,
            JanelaSegundos = regra.JanelaSegundos,
            Ativa = regra.Ativa
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var resultado = await servicoRegras.SalvarAsync(Id, new DadosRegra(
            Dados.Nome, Dados.Descricao, Dados.Fonte, Dados.Criterio, Dados.Padrao,
            Dados.LimiteOcorrencias, Dados.JanelaSegundos, Dados.Ativa));

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            return Page();
        }

        MensagemSucesso = "Regra salva.";
        return RedirectToPage("/Regras/Index");
    }

    public sealed class DadosFormulario
    {
        [Required(ErrorMessage = "Campo obrigatório.")]
        [StringLength(100)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Descrição")]
        public string? Descricao { get; set; }

        [Display(Name = "Fonte")]
        public TipoFonte Fonte { get; set; } = TipoFonte.LogIis;

        [Display(Name = "Critério")]
        public TipoCriterio Criterio { get; set; } = TipoCriterio.CodigoStatus;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [StringLength(1000)]
        [Display(Name = "Padrão")]
        public string Padrao { get; set; } = string.Empty;

        [Range(1, 100000, ErrorMessage = "Valor fora do intervalo permitido.")]
        [Display(Name = "Limite de ocorrências")]
        public int LimiteOcorrencias { get; set; } = 10;

        [Range(1, 86400, ErrorMessage = "Valor fora do intervalo permitido.")]
        [Display(Name = "Janela (segundos)")]
        public int JanelaSegundos { get; set; } = 60;

        [Display(Name = "Ativa")]
        public bool Ativa { get; set; } = true;
    }
}
