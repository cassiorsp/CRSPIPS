using System.Security.Claims;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Web.Infra;

internal sealed class ContextoUsuarioHttp(IHttpContextAccessor acessor) : IContextoUsuario
{
    public string Nome => acessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? "anônimo";

    public string? Ip
    {
        get
        {
            var remoto = acessor.HttpContext?.Connection.RemoteIpAddress;
            return remoto is null ? null : EnderecoIp.Criar(remoto).ToString();
        }
    }
}

internal static class PoliticasLimite
{
    public const string Login = "login";
}

/// <summary>Cabecalhos de seguranca. Scripts inline sao proibidos pelo CSP: os dados chegam ao JS via JSON em &lt;script type="application/json"&gt;.</summary>
internal sealed class CabecalhosSegurancaMiddleware(RequestDelegate proximo)
{
    private const string Cdn = "https://cdn.jsdelivr.net";

    public Task InvokeAsync(HttpContext contexto)
    {
        var cabecalhos = contexto.Response.Headers;
        cabecalhos.XContentTypeOptions = "nosniff";
        cabecalhos.XFrameOptions = "DENY";
        cabecalhos["Referrer-Policy"] = "no-referrer";
        cabecalhos.ContentSecurityPolicy =
            $"default-src 'self'; script-src 'self' {Cdn}; style-src 'self' 'unsafe-inline' {Cdn}; " +
            $"img-src 'self' data: {Cdn}; font-src 'self' {Cdn}; connect-src 'self'; frame-ancestors 'none'; form-action 'self'";
        return proximo(contexto);
    }
}
