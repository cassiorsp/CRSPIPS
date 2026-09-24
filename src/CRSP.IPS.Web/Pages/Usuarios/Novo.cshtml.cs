using System.ComponentModel.DataAnnotations;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Usuarios;

public sealed class NovoModel(ServicoUsuarios servicoUsuarios) : PaginaBase
{
    [BindProperty]
    public DadosUsuario Dados { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var resultado = await servicoUsuarios.CriarAsync(Dados.Nome, Dados.Email, Dados.Senha);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            return Page();
        }

        MensagemSucesso = "Usuário criado.";
        return RedirectToPage("/Usuarios/Index");
    }

    public sealed class DadosUsuario
    {
        [Required(ErrorMessage = "Campo obrigatório.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Senha), ErrorMessage = "As senhas não conferem.")]
        [Display(Name = "Confirmar senha")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }
}
