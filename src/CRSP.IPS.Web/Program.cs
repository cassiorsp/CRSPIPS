using System.Globalization;
using System.Threading.RateLimiting;
using CRSP.IPS.Application;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Infrastructure;
using CRSP.IPS.Infrastructure.Persistencia;
using CRSP.IPS.Web;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AdicionarInfraestrutura(builder.Configuration)
    .AdicionarAplicacao();

// Chaves do cookie de login e dos tokens antiforgery persistidas em disco (protegidas por DPAPI).
// Sem isso, em um app pool sem perfil de usuario, cada reciclagem desloga todos e invalida formularios abertos.
var pastaDados = Path.GetDirectoryName(Path.GetFullPath(
    builder.Configuration[$"{OpcoesCrspips.Secao}:{nameof(OpcoesCrspips.CaminhoBanco)}"] ?? new OpcoesCrspips().CaminhoBanco))!;
builder.Services.AddDataProtection()
    .SetApplicationName("CRSPIPS")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaDados, "chaves")))
    .ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContextoUsuario, ContextoUsuarioHttp>();

builder.Services.AddLocalization(opcoes => opcoes.ResourcesPath = "Recursos");
builder.Services
    .AddRazorPages(opcoes =>
    {
        opcoes.Conventions.AuthorizeFolder("/");
        opcoes.Conventions.AllowAnonymousToPage("/Conta/Entrar");
        opcoes.Conventions.AllowAnonymousToPage("/Conta/PrimeiroAcesso");
        opcoes.Conventions.AllowAnonymousToPage("/Erro");
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(opcoes =>
        opcoes.DataAnnotationLocalizerProvider = (_, fabrica) => fabrica.Create(typeof(Textos)));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/Conta/Entrar";
        opcoes.LogoutPath = "/Conta/Sair";
        opcoes.AccessDeniedPath = "/Conta/Entrar";
        opcoes.Cookie.Name = "CRSPIPS.Auth";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Strict;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
        opcoes.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddPolicy(PoliticasLimite.Login, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

await InicializadorBanco.InicializarAsync(app.Services);

var culturas = new[] { new CultureInfo("pt-BR"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("pt-BR"),
    SupportedCultures = culturas,
    SupportedUICultures = culturas,
    RequestCultureProviders = [new CookieRequestCultureProvider(), new AcceptLanguageHeaderRequestCultureProvider()]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Erro");
    app.UseHsts();
}

app.UseMiddleware<CabecalhosSegurancaMiddleware>();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.MapGet("/idioma", (string cultura, string? retorno, HttpContext contexto) =>
{
    if (culturas.Any(c => c.Name == cultura))
    {
        contexto.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(cultura)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
    }

    return Results.LocalRedirect(string.IsNullOrEmpty(retorno) || !retorno.StartsWith('/') || retorno.StartsWith("//") ? "/" : retorno);
}).AllowAnonymous();

app.Run();

public partial class Program;
