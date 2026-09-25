using System.Security.Claims;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// Quem esta operando o painel (usado na auditoria e na protecao do proprio IP). Em requisicoes HTTP (login, prerender)
/// vem do HttpContext. No circuito do Blazor o HttpContext nao e confiavel, entao <see cref="CapturaUsuarioCircuito"/>
/// grava nome e IP quando a conexao abre, e <see cref="ExecutorServicos"/> repassa para cada operacao.
/// </summary>
public sealed class ContextoUsuario(IHttpContextAccessor acessor) : IContextoUsuario
{
    private string? _nome;
    private string? _ip;

    public string Nome => _nome ?? acessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? "anônimo";

    public string? Ip => _ip ?? (acessor.HttpContext?.Connection.RemoteIpAddress is { } remoto ? EnderecoIp.Criar(remoto).ToString() : null);

    public void Definir(string? nome, string? ip)
    {
        _nome = nome;
        _ip = ip;
    }

    public void CopiarDe(ContextoUsuario origem) => Definir(origem.Nome, origem.Ip);
}

/// <summary>Grava o usuario e o IP da conexao SignalR no contexto do circuito.</summary>
internal sealed class CapturaUsuarioCircuito(ContextoUsuario contexto, IHttpContextAccessor acessor) : CircuitHandler
{
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (acessor.HttpContext is { User.Identity.IsAuthenticated: true } http)
        {
            var remoto = http.Connection.RemoteIpAddress;
            contexto.Definir(http.User.FindFirstValue(ClaimTypes.Email), remoto is null ? null : EnderecoIp.Criar(remoto).ToString());
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Executa cada operacao do painel em um escopo de DI proprio. No Blazor Server o escopo do circuito dura a sessao
/// inteira; reaproveitar o mesmo DbContext acumularia entidades rastreadas e mostraria dados desatualizados.
/// </summary>
public sealed class ExecutorServicos(IServiceScopeFactory fabrica, ContextoUsuario contexto)
{
    public async Task<TResultado> ExecutarAsync<TServico, TResultado>(Func<TServico, Task<TResultado>> operacao)
        where TServico : notnull
    {
        await using var escopo = fabrica.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<ContextoUsuario>().CopiarDe(contexto);
        return await operacao(escopo.ServiceProvider.GetRequiredService<TServico>());
    }
}
