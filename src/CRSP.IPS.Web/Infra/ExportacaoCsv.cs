using System.Globalization;
using System.Text;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using Microsoft.Extensions.Localization;

namespace CRSP.IPS.Web.Infra;

/// <summary>
/// Exportacoes CSV das tabelas (Endpoints, sites do IIS e Bloqueios), com os mesmos filtros e ordenacao da tela.
/// Ponto e virgula e UTF-8 com BOM, para o Excel em pt-BR abrir direto.
/// </summary>
public static class ExportacaoCsv
{
    public static void MapearExportacoesCsv(this WebApplication app)
    {
        app.MapGet("/Endpoints/exportar.csv", async (
            string? periodo, string? site, string? busca, bool? erros, string? ordem, bool? crescente,
            ServicoMetricasPainel servico, IStringLocalizer<Textos> t, CancellationToken ct) =>
        {
            var painel = await servico.ObterEndpointsAsync(ConverterPeriodo(periodo), string.IsNullOrWhiteSpace(site) ? null : site, ct);
            var linhas = OrdenacaoMetricas.Ordenar(OrdenacaoMetricas.Filtrar(painel.Endpoints, busca, erros == true), ordem, crescente != true);

            var csv = new EscritorCsv();
            csv.Linha(t["Site"], t["Método"], t["Endpoint"], t["Total"], t["Sem erro"], "4xx", "5xx", t["% erro"], t["Tempo médio"] + " (ms)", t["Tempo máx."] + " (ms)");
            foreach (var l in linhas)
                csv.Linha(l.Site, l.Metodo, l.Endpoint, l.Total, l.Sucesso + l.Redirecionamento, l.ErroCliente, l.ErroServidor, l.PercentualErro, l.TempoMedioMs, l.TempoMaximoMs);
            return csv.Arquivo("crspips-endpoints");
        }).RequireAuthorization();

        app.MapGet("/Iis/sites.csv", async (
            string? periodo, string? ordem, bool? crescente,
            ServicoMetricasPainel servico, IStringLocalizer<Textos> t, CancellationToken ct) =>
        {
            var painel = await servico.ObterIisAsync(ConverterPeriodo(periodo), null, ct);

            var csv = new EscritorCsv();
            csv.Linha(t["Site"], t["Application pool"], t["Acessos"], t["Erros 5xx"], t["Tempo médio"] + " (ms)",
                t["Memória mín."] + " (MB)", t["Memória média"] + " (MB)", t["Memória máx."] + " (MB)", t["CPU média"] + " (%)");
            foreach (var s in OrdenacaoMetricas.Ordenar(painel.Sites, ordem, crescente != true))
            {
                csv.Linha(s.Site, s.Pool, s.Requisicoes.Total, s.Requisicoes.ErroServidor, s.Requisicoes.TempoMedioMs,
                    EmMb(s.Consumo?.MemoriaMinima), EmMb(s.Consumo?.MemoriaMedia), EmMb(s.Consumo?.MemoriaMaxima), s.Consumo?.CpuMedia);
            }

            return csv.Arquivo("crspips-iis-sites");
        }).RequireAuthorization();

        app.MapGet("/Bloqueios/exportar.csv", async (
            string? busca, string? status, string? origem, string? pais, int? regraId, bool? simulado, DateOnly? de, DateOnly? ate,
            ServicoBloqueios servico, IStringLocalizer<Textos> t, CancellationToken ct) =>
        {
            var bloqueios = await servico.ListarParaExportacaoAsync(MontarFiltroBloqueios(busca, status, origem, pais, regraId, simulado, de, ate), ct);

            var csv = new EscritorCsv();
            csv.Linha(t["IP"], t["País"], t["Cidade"], "ASN", t["Organização"], t["Motivo"], t["Origem"], t["Regra"], t["Ocorrências"], t["Nível"],
                t["Bloqueado em"], t["Expira em"], t["Encerrado em"], t["Situação"], t["Criado por"], t["Comentário"]);
            foreach (var b in bloqueios)
            {
                csv.Linha(b.Ip, Exibicao.NomePais(b.PaisCodigo, b.PaisNome), b.Cidade, b.Asn, b.Organizacao, t[b.Motivo], t[Exibicao.Rotulo(b.Origem)],
                    b.Regra?.Nome, b.Ocorrencias, b.NivelReincidencia, b.BloqueadoEm,
                    b.Status == StatusBloqueio.Ativo && b.EhPermanente ? t["Permanente"].Value : (object?)b.ExpiraEm,
                    b.EncerradoEm, t[Exibicao.SituacaoBloqueio(b).Texto], b.CriadoPor, b.Comentario);
            }

            return csv.Arquivo("crspips-bloqueios");
        }).RequireAuthorization();
    }

    /// <summary>Filtro da tela Bloqueios a partir dos parametros da URL (datas no horario local do servidor).</summary>
    public static FiltroBloqueios MontarFiltroBloqueios(
        string? busca, string? status, string? origem, string? pais, int? regraId, bool? simulado, DateOnly? de, DateOnly? ate) => new()
    {
        Busca = busca,
        Status = Enum.TryParse<StatusBloqueio>(status, out var statusLido) ? statusLido : null,
        Origem = Enum.TryParse<OrigemBloqueio>(origem, out var origemLida) ? origemLida : null,
        PaisCodigo = pais,
        RegraId = regraId,
        Simulado = simulado,
        DeUtc = ParaUtc(de),
        AteUtc = ParaUtc(ate?.AddDays(1))
    };

    private static DateTime? ParaUtc(DateOnly? data) =>
        data is null ? null : TimeZoneInfo.ConvertTimeToUtc(data.Value.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local);

    private static PeriodoDashboard ConverterPeriodo(string? texto) =>
        Enum.GetValues<PeriodoDashboard>().FirstOrDefault(p => Exibicao.Rotulo(p).Equals(texto, StringComparison.OrdinalIgnoreCase), PeriodoDashboard.Hoje);

    private static double? EmMb(long? bytes) => bytes is null ? null : bytes.Value / (1024d * 1024d);

    private sealed class EscritorCsv
    {
        private readonly StringBuilder _texto = new();

        public void Linha(params object?[] campos) =>
            _texto.AppendJoin(';', campos.Select(Formatar)).Append("\r\n");

        public IResult Arquivo(string nome) => Results.File(
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_texto.ToString())).ToArray(),
            "text/csv; charset=utf-8",
            $"{nome}-{DateTime.Now:yyyyMMdd-HHmm}.csv");

        private static string Formatar(object? campo) => campo switch
        {
            null => string.Empty,
            DateTime utc => TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            double numero => numero.ToString("0.##", CultureInfo.CurrentCulture),
            IFormattable numero => numero.ToString(null, CultureInfo.InvariantCulture),
            _ => Texto(campo.ToString() ?? string.Empty)
        };

        private static string Texto(string valor)
        {
            // URLs e motivos vem do trafego: o apostrofo impede o Excel de executar o campo como formula.
            if (valor.Length > 0 && valor[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                valor = "'" + valor;
            return valor.AsSpan().IndexOfAny(";\"\r\n") >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
        }
    }
}
