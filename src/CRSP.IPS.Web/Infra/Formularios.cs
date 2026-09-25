using System.ComponentModel.DataAnnotations;

namespace CRSP.IPS.Web.Infra;

/// <summary>Formularios compartilhados. As mensagens de erro sao chaves pt-BR (traduzidas por MensagemValidacao).</summary>
public sealed class DadosNovoUsuario
{
    [Required(ErrorMessage = "Campo obrigatório.")]
    [StringLength(150, ErrorMessage = "Texto muito longo.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Campo obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Campo obrigatório.")]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "Campo obrigatório.")]
    [Compare(nameof(Senha), ErrorMessage = "As senhas não conferem.")]
    public string ConfirmarSenha { get; set; } = string.Empty;
}
