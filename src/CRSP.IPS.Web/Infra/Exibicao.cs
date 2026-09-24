using System.Globalization;
using System.Text.Json;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.AspNetCore.Html;

namespace CRSP.IPS.Web.Infra;

/// <summary>Formatacao para as telas. Textos retornados sao chaves pt-BR (traduzidas na view com T[...]).</summary>
public static class Exibicao
{
    /// <summary>
    /// Nomes de paises em pt-BR e en (gerados a partir do ICU). Tabela embutida porque RegionInfo.DisplayName
    /// devolve o nome nativo em alguns sistemas (ex.: "中国" em vez de "China").
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<string, string[]>> NomesPaises = new(() =>
    {
        using var fluxo = typeof(Exibicao).Assembly.GetManifestResourceStream("CRSPIPS.paises.json")
            ?? throw new InvalidOperationException("Recurso paises.json nao encontrado.");
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(fluxo)!;
    });

    public static string DataHora(DateTime? utc) =>
        utc is null ? "—" : TimeZoneInfo.ConvertTimeFromUtc(utc.Value, TimeZoneInfo.Local).ToString("g", CultureInfo.CurrentCulture);

    public static string Duracao(TimeSpan duracao) => DuracaoTexto.Formatar(duracao);

    public static string NomePais(string? codigo, string? nomeAlternativo = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return nomeAlternativo ?? "—";
        if (!NomesPaises.Value.TryGetValue(codigo.ToUpperInvariant(), out var nomes))
            return nomeAlternativo ?? codigo;
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? nomes[1] : nomes[0];
    }

    public static IHtmlContent Bandeira(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) || codigo.Length != 2
            ? new HtmlString("<span class=\"fi fi-xx bandeira-vazia\"></span>")
            : new HtmlString($"<span class=\"fi fi-{codigo.ToLowerInvariant()}\" title=\"{codigo.ToUpperInvariant()}\"></span>");

    /// <summary>Todos os paises (codigo ISO + nome no idioma atual), ordenados pelo nome.</summary>
    public static IReadOnlyList<(string Codigo, string Nome)> ListarPaises() =>
        NomesPaises.Value.Keys.Select(c => (c, NomePais(c))).OrderBy(p => p.Item2, StringComparer.CurrentCulture).ToList();

    public static (string Classe, string Texto) SituacaoBloqueio(Bloqueio bloqueio) => bloqueio switch
    {
        { Status: StatusBloqueio.Ativo, Simulado: true } => ("text-bg-info", "Simulado"),
        { EstaPendenteNoFirewall: true } => ("text-bg-warning", "Pendente"),
        { Status: StatusBloqueio.Ativo } => ("text-bg-danger", "Bloqueado"),
        { Status: StatusBloqueio.Expirado } => ("text-bg-secondary", "Expirado"),
        _ => ("text-bg-success", "Liberado")
    };

    public static string Rotulo(StatusBloqueio valor) => valor switch
    {
        StatusBloqueio.Ativo => "Ativo",
        StatusBloqueio.Expirado => "Expirado",
        _ => "Liberado"
    };

    public static string Rotulo(OrigemBloqueio valor) => valor switch
    {
        OrigemBloqueio.Automatico => "Automático",
        OrigemBloqueio.Manual => "Manual",
        _ => "País"
    };

    public static string Rotulo(TipoFonte valor) => valor switch
    {
        TipoFonte.LogIis => "Log IIS",
        TipoFonte.HttpErr => "HTTPERR",
        _ => "Evento Windows"
    };

    public static string Rotulo(TipoCriterio valor) => valor switch
    {
        TipoCriterio.CodigoStatus => "Código HTTP",
        TipoCriterio.PadraoUrl => "Padrão de URL (regex)",
        TipoCriterio.MotivoHttpErr => "Motivo HTTPERR (regex)",
        _ => "ID de evento do Windows"
    };

    public static string Rotulo(ModoPoliticaPaises valor) => valor switch
    {
        ModoPoliticaPaises.Desativada => "Desativada",
        ModoPoliticaPaises.BloquearListados => "Bloquear países listados",
        _ => "Permitir somente países listados"
    };

    public static string Rotulo(AplicacaoPoliticaPaises valor) => valor switch
    {
        AplicacaoPoliticaPaises.Reativa => "Reativa (todo o tráfego)",
        AplicacaoPoliticaPaises.ReativaSuspeitos => "Reativa (somente eventos suspeitos)",
        _ => "Firewall por portas"
    };

    public static IReadOnlyList<(string Valor, string Texto)> OpcoesDuracao { get; } =
    [
        ("1h", "1 hora"),
        ("6h", "6 horas"),
        ("24h", "24 horas"),
        ("7d", "7 dias"),
        ("30d", "30 dias"),
        ("permanente", "Permanente")
    ];

    public static bool TentarConverterDuracao(string? valor, out TimeSpan? duracao)
    {
        duracao = null;
        if (valor == "permanente")
            return true;
        if (!DuracaoTexto.TentarConverter(valor, out var convertida))
            return false;
        duracao = convertida;
        return true;
    }
}
