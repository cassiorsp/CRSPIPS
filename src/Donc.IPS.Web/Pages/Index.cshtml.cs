using System.Text.Json;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Web.Infra;
using Microsoft.Extensions.Localization;

namespace Donc.IPS.Web.Pages;

public sealed class IndexModel(ServicoPainel servicoPainel, IStringLocalizer<Textos> textos) : PaginaBase
{
    public IndicadoresDashboard Indicadores { get; private set; } = null!;

    /// <summary>Dados dos graficos, entregues ao JavaScript em um bloco JSON (o CSP proibe script inline).</summary>
    public string DadosGraficosJson { get; private set; } = "{}";

    public async Task OnGetAsync(CancellationToken ct)
    {
        Indicadores = await servicoPainel.ObterIndicadoresAsync(ct);

        DadosGraficosJson = JsonSerializer.Serialize(new
        {
            horas = Indicadores.SerieEventos.Select(p => p.Rotulo),
            eventos = Indicadores.SerieEventos.Select(p => p.Quantidade),
            bloqueios = Indicadores.SerieBloqueios.Select(p => p.Quantidade),
            rotuloEventos = textos["Eventos suspeitos"].Value,
            rotuloBloqueios = textos["Bloqueios"].Value,
            fontes = Indicadores.EventosPorFonte.Select(f => textos[RotuloFonte(f.Rotulo)].Value),
            fontesQuantidade = Indicadores.EventosPorFonte.Select(f => f.Quantidade),
            paises = Indicadores.TopPaises.Select(p => Exibicao.NomePais(p.Rotulo, p.Complemento)),
            paisesQuantidade = Indicadores.TopPaises.Select(p => p.Quantidade)
        });
    }

    private static string RotuloFonte(string fonte) =>
        Enum.TryParse<Domain.Enums.TipoFonte>(fonte, out var tipo) ? Exibicao.Rotulo(tipo) : fonte;
}
