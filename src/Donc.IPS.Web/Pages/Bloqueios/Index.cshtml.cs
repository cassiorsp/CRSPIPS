using Donc.IPS.Application.Modelos;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Donc.IPS.Web.Pages.Bloqueios;

public sealed class IndexModel(ServicoBloqueios servicoBloqueios, ServicoListas servicoListas, ServicoRegras servicoRegras) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string? Busca { get; set; }
    [BindProperty(SupportsGet = true)] public StatusBloqueio? Status { get; set; }
    [BindProperty(SupportsGet = true)] public OrigemBloqueio? Origem { get; set; }
    [BindProperty(SupportsGet = true)] public string? Pais { get; set; }
    [BindProperty(SupportsGet = true)] public int? RegraId { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Simulado { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? De { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Ate { get; set; }
    [BindProperty(SupportsGet = true)] public int Pagina { get; set; } = 1;

    public Pagina<Bloqueio> Resultado { get; private set; } = null!;
    public IReadOnlyList<RegraDeteccao> Regras { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Regras = await servicoRegras.ListarAsync(ct);
        Resultado = await servicoBloqueios.PesquisarAsync(new FiltroBloqueios
        {
            Busca = Busca,
            Status = Status,
            Origem = Origem,
            PaisCodigo = Pais,
            RegraId = RegraId,
            Simulado = Simulado,
            DeUtc = ParaUtc(De),
            AteUtc = ParaUtc(Ate?.AddDays(1)),
            Pagina = Pagina
        }, ct);
    }

    public async Task<IActionResult> OnPostBloquearAsync(string ip, string duracao, string? comentario, string? retorno)
    {
        if (!Exibicao.TentarConverterDuracao(duracao, out var tempo))
            return Concluir(Application.Modelos.Resultado.Falha("Duração inválida."), string.Empty, retorno);
        return Concluir(await servicoBloqueios.BloquearManualAsync(ip, tempo, comentario),
            "Bloqueio registrado. O firewall será atualizado em instantes.", retorno);
    }

    public async Task<IActionResult> OnPostDesbloquearAsync(int id, string? comentario, string? retorno) =>
        Concluir(await servicoBloqueios.DesbloquearAsync(id, comentario),
            "IP liberado. O firewall será atualizado em instantes.", retorno);

    public async Task<IActionResult> OnPostDesbloquearSelecionadosAsync(int[] ids, string? comentario, string? retorno) =>
        Concluir(await servicoBloqueios.DesbloquearVariosAsync(ids, comentario),
            "IPs selecionados liberados.", retorno);

    public async Task<IActionResult> OnPostAlterarExpiracaoAsync(int id, string duracao, string? retorno)
    {
        if (!Exibicao.TentarConverterDuracao(duracao, out var tempo))
            return Concluir(Application.Modelos.Resultado.Falha("Duração inválida."), string.Empty, retorno);
        return Concluir(await servicoBloqueios.AlterarExpiracaoAsync(id, tempo), "Expiração alterada.", retorno);
    }

    public async Task<IActionResult> OnPostListaBrancaAsync(string ip, string? retorno) =>
        Concluir(await servicoListas.AdicionarAsync(TipoLista.Branca, ip, "Incluído pela tela de bloqueios"),
            "IP adicionado à lista branca e liberado.", retorno);

    private static DateTime? ParaUtc(DateOnly? data) =>
        data is null ? null : TimeZoneInfo.ConvertTimeToUtc(data.Value.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local);
}
