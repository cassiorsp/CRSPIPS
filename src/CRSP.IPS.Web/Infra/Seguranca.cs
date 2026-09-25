using System.Threading.RateLimiting;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// Limite de tentativas de login por IP (10 por minuto). As telas de acesso sao componentes com formulario,
/// entao o limite e consultado no envio em vez de um middleware de rate limiting por endpoint.
/// </summary>
public sealed class LimitadorLogin : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limitador = PartitionedRateLimiter.Create<string, string>(ip =>
        RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    public bool Permitir(string? ip)
    {
        using var permissao = _limitador.AttemptAcquire(ip ?? "desconhecido");
        return permissao.IsAcquired;
    }

    public void Dispose() => _limitador.Dispose();
}

/// <summary>
/// Cabecalhos de seguranca. Sem script inline; a unica origem externa e o jsDelivr (Chart.js, Bootstrap Icons,
/// bandeiras SVG e fonte Inter). O Blazor usa WebSocket na mesma origem, coberto por connect-src 'self'.
/// </summary>
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
