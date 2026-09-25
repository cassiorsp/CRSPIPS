using System.Globalization;
using System.Text;
using CRSP.IPS.Application;
using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Infrastructure;
using CRSP.IPS.Infrastructure.Persistencia;
using CRSP.IPS.Web.Componentes;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

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
builder.Services.AddScoped<ContextoUsuario>();
builder.Services.AddScoped<IContextoUsuario>(provedor => provedor.GetRequiredService<ContextoUsuario>());
builder.Services.AddScoped<CircuitHandler, CapturaUsuarioCircuito>();
builder.Services.AddScoped<ExecutorServicos>();
builder.Services.AddScoped<Avisos>();
builder.Services.AddSingleton<LimitadorLogin>();

builder.Services.AddLocalization(opcoes => opcoes.ResourcesPath = "Recursos");
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/Conta/Entrar";
        opcoes.AccessDeniedPath = "/Conta/Entrar";
        opcoes.Cookie.Name = "CRSPIPS.Auth";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Strict;
        opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(8);
        opcoes.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

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
    app.UseExceptionHandler("/Erro", createScopeForErrors: true);
    app.UseHsts();
}

app.UseMiddleware<CabecalhosSegurancaMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
// O Blazor enviaria um segundo Content-Security-Policy (frame-ancestors 'self'); o nosso ja define frame-ancestors 'none'.
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(opcoes => opcoes.ContentSecurityFrameAncestorsPolicy = null);

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

// Formulario com token antiforgery no menu do usuario (o parametro [FromForm] ativa a validacao do token).
app.MapPost("/Conta/Sair", async (HttpContext contexto, [FromForm] string? origem) =>
{
    await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/Conta/Entrar");
}).RequireAuthorization();

// UTF-8 com BOM para o Excel abrir os acentos corretamente.
app.MapGet("/Listas/modelo.csv", () =>
    Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ArquivoCsvListas.GerarModelo())).ToArray(),
        "text/csv; charset=utf-8", "crspips-modelo-lista.csv"))
    .RequireAuthorization();

app.Run();

public partial class Program;
