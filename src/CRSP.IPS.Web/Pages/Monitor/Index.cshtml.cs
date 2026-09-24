using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace CRSP.IPS.Web.Pages.Monitor;

public sealed class IndexModel(ServicoPainel servicoPainel, IServicoGeolocalizacao geolocalizacao, IStringLocalizer<Textos> textos) : PaginaBase
{
    public void OnGet() { }

    /// <summary>Consultado pelo monitor.js a cada 5 segundos, retornando so os eventos novos.</summary>
    public async Task<IActionResult> OnGetEventosAsync(TipoFonte? fonte, long? aposId, CancellationToken ct)
    {
        var eventos = await servicoPainel.ListarEventosRecentesAsync(fonte, aposId, ct);
        var localizacoes = new Dictionary<string, string?>();

        string? DescreverLocal(string ip)
        {
            if (localizacoes.TryGetValue(ip, out var descricao))
                return descricao;

            var local = EnderecoIp.TentarConverter(ip, out var endereco) ? geolocalizacao.Localizar(endereco) : null;
            descricao = local is null
                ? null
                : string.Join(" · ", new[]
                {
                    Exibicao.NomePais(local.PaisCodigo, local.PaisNome),
                    local.Cidade,
                    local.Asn is { } asn ? $"AS{asn} {local.Organizacao}".Trim() : null
                }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
            localizacoes[ip] = descricao;
            return descricao;
        }

        return new JsonResult(eventos.Select(e => new
        {
            e.Id,
            e.Ip,
            pais = e.PaisCodigo?.ToLowerInvariant(),
            local = DescreverLocal(e.Ip),
            quando = Exibicao.DataHora(e.OcorridoEmUtc),
            fonte = textos[Exibicao.Rotulo(e.Fonte)].Value,
            regra = e.Regra ?? textos["Política de países"].Value,
            site = e.Site,
            requisicao = string.Join(' ', new[] { e.Metodo, e.Url }.Where(v => v is not null)),
            status = e.CodigoStatus,
            detalhe = e.Detalhe ?? e.UserAgent
        }));
    }
}
