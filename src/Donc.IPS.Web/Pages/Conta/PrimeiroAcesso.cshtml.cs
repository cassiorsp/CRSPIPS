using System.ComponentModel.DataAnnotations;
using System.Net;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Donc.IPS.Web.Pages.Conta;

/// <summary>
/// Cria o primeiro administrador. So funciona enquanto nao existe nenhum usuario E a requisicao vem
/// do proprio servidor (localhost), para que ninguem de fora assuma o painel recem-instalado.
/// </summary>
[EnableRateLimiting(PoliticasLimite.Login)]
public sealed class PrimeiroAcessoModel(ServicoUsuarios servicoUsuarios, IContextoUsuario contextoIp) : PaginaBase
{
    [BindProperty]
    public DadosAdministrador Dados { get; set; } = new();

    public bool AcessoLocal => HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);

    public async Task<IActionResult> OnGetAsync() =>
        await servicoUsuarios.ExisteAlgumAsync() ? RedirectToPage("/Conta/Entrar") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await servicoUsuarios.ExisteAlgumAsync())
            return RedirectToPage("/Conta/Entrar");
        if (!AcessoLocal)
            return Page();
        if (!ModelState.IsValid)
            return Page();

        var criacao = await servicoUsuarios.CriarAsync(Dados.Nome, Dados.Email, Dados.Senha);
        if (!criacao.Sucesso)
        {
            ModelState.AddModelError(string.Empty, criacao.Erro!);
            return Page();
        }

        var autenticacao = await servicoUsuarios.AutenticarAsync(Dados.Email, Dados.Senha, contextoIp.Ip);
        if (autenticacao.Valor is not null)
            await EntrarModel.AutenticarAsync(HttpContext, autenticacao.Valor);

        MensagemSucesso = "Administrador criado. O sistema está em modo simulação: revise as regras antes de ativar os bloqueios.";
        return RedirectToPage("/Index");
    }

    public sealed class DadosAdministrador
    {
        [Required(ErrorMessage = "Campo obrigatório.")]
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
