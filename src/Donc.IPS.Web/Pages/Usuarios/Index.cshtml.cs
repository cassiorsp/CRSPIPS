using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Donc.IPS.Web.Pages.Usuarios;

public sealed class IndexModel(ServicoUsuarios servicoUsuarios) : PaginaBase
{
    public IReadOnlyList<Usuario> Usuarios { get; private set; } = [];

    public int IdLogado => IdUsuarioLogado;

    public async Task OnGetAsync(CancellationToken ct) => Usuarios = await servicoUsuarios.ListarAsync(ct);

    public async Task<IActionResult> OnPostAlternarAsync(int id) =>
        Concluir(await servicoUsuarios.AlternarAtivoAsync(id, IdUsuarioLogado), "Usuário atualizado.");

    public async Task<IActionResult> OnPostRedefinirSenhaAsync(int id, string novaSenha) =>
        Concluir(await servicoUsuarios.AlterarSenhaAsync(id, novaSenha), "Senha redefinida.");
}
