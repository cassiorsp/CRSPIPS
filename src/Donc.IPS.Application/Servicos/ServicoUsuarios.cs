using System.Net.Mail;
using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Servicos;

public sealed class ServicoUsuarios(
    IRepositorioUsuarios usuarios,
    IHashSenha hashSenha,
    ServicoAuditoria auditoria,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoUsuarios> logger)
{
    public const int TamanhoMinimoSenha = 10;

    public Task<bool> ExisteAlgumAsync(CancellationToken ct = default) => usuarios.ExisteAlgumAsync(ct);

    public Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken ct = default) => usuarios.ListarAsync(ct);

    public async Task<Resultado<Usuario>> AutenticarAsync(string email, string senha, string? ip, CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var usuario = await usuarios.ObterPorEmailAsync(Usuario.NormalizarEmail(email ?? string.Empty), ct);

        if (usuario is null || !usuario.Ativo)
        {
            logger.LogWarning("Falha de login para {Email} a partir de {Ip}", email, ip);
            return Resultado<Usuario>.Falha("E-mail ou senha inválidos.");
        }

        if (usuario.EstaTemporariamenteBloqueado(agora))
            return Resultado<Usuario>.Falha("Conta temporariamente bloqueada por tentativas inválidas. Tente novamente em alguns minutos.");

        if (!hashSenha.Verificar(usuario.HashSenha, senha ?? string.Empty))
        {
            usuario.RegistrarFalhaLogin(agora);
            await unidadeDeTrabalho.SalvarAsync(ct);
            logger.LogWarning("Senha invalida para {Email} a partir de {Ip}", usuario.Email, ip);
            return Resultado<Usuario>.Falha("E-mail ou senha inválidos.");
        }

        usuario.RegistrarAcesso(agora, ip);
        await unidadeDeTrabalho.SalvarAsync(ct);
        logger.LogInformation("Login de {Email} a partir de {Ip}", usuario.Email, ip);
        return Resultado<Usuario>.Ok(usuario);
    }

    public async Task<Resultado> CriarAsync(string nome, string email, string senha, CancellationToken ct = default)
    {
        var erro = ValidarDados(nome, email) ?? ValidarSenha(senha);
        if (erro is not null)
            return Resultado.Falha(erro);

        if (await usuarios.ObterPorEmailAsync(Usuario.NormalizarEmail(email), ct) is not null)
            return Resultado.Falha("Já existe um usuário com este e-mail.");

        var usuario = Usuario.Criar(nome, email, hashSenha.Gerar(senha), relogio.GetUtcNow().UtcDateTime);
        usuarios.Adicionar(usuario);
        auditoria.Registrar("Usuário criado", usuario.Email);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> AlterarSenhaAsync(int id, string novaSenha, CancellationToken ct = default)
    {
        var erro = ValidarSenha(novaSenha);
        if (erro is not null)
            return Resultado.Falha(erro);

        var usuario = await usuarios.ObterPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado.Falha("Usuário não encontrado.");

        usuario.AlterarSenha(hashSenha.Gerar(novaSenha));
        auditoria.Registrar("Senha alterada", usuario.Email);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> AlterarPropriaSenhaAsync(int id, string senhaAtual, string novaSenha, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado.Falha("Usuário não encontrado.");
        if (!hashSenha.Verificar(usuario.HashSenha, senhaAtual ?? string.Empty))
            return Resultado.Falha("A senha atual está incorreta.");

        return await AlterarSenhaAsync(id, novaSenha, ct);
    }

    public async Task<Resultado> AlternarAtivoAsync(int id, int idUsuarioLogado, CancellationToken ct = default)
    {
        if (id == idUsuarioLogado)
            return Resultado.Falha("Você não pode desativar o seu próprio usuário.");

        var usuario = await usuarios.ObterPorIdAsync(id, ct);
        if (usuario is null)
            return Resultado.Falha("Usuário não encontrado.");

        usuario.DefinirAtivo(!usuario.Ativo);
        auditoria.Registrar(usuario.Ativo ? "Usuário ativado" : "Usuário desativado", usuario.Email);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    private static string? ValidarDados(string nome, string email)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Informe o nome.";
        if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email.Trim(), out _))
            return "Informe um e-mail válido.";
        return null;
    }

    private static string? ValidarSenha(string senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < TamanhoMinimoSenha)
            return "A senha deve ter no mínimo 10 caracteres.";
        if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit))
            return "A senha deve conter letras e números.";
        return null;
    }
}
