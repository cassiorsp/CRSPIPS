using System.ComponentModel.DataAnnotations;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Conta;

public sealed class SenhaModel(ServicoUsuarios servicoUsuarios) : PaginaBase
{
    [BindProperty]
    public DadosSenha Dados { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var resultado = await servicoUsuarios.AlterarPropriaSenhaAsync(IdUsuarioLogado, Dados.SenhaAtual, Dados.NovaSenha);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            return Page();
        }

        MensagemSucesso = "Senha alterada.";
        return RedirectToPage("/Index");
    }

    public sealed class DadosSenha
    {
        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha atual")]
        public string SenhaAtual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nova senha")]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem.")]
        [Display(Name = "Confirmar senha")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }
}
