using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Servicos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;
using Donc.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace Donc.IPS.Web.Pages.Listas;

public sealed class ExternasModel(ServicoListasExternas servicoListasExternas, IServicoGeolocalizacao geolocalizacao) : PaginaBase
{
    public IReadOnlyList<ListaExterna> Listas { get; private set; } = [];
    public IReadOnlyList<CoincidenciaListaExterna> Coincidencias { get; private set; } = [];
    public bool ModoSimulacao { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Listas = await servicoListasExternas.ListarAsync(ct);
        Coincidencias = await servicoListasExternas.ListarCoincidenciasAsync(ct);
        ModoSimulacao = await servicoListasExternas.EstaEmSimulacaoAsync(ct);
    }

    public async Task<IActionResult> OnPostModoAsync(int id, ModoListaExterna modo) =>
        Concluir(await servicoListasExternas.DefinirModoAsync(id, modo), "Lista externa atualizada. O motor aplica a alteração em instantes.");

    public async Task<IActionResult> OnPostAtualizarAsync(int id) =>
        Concluir(await servicoListasExternas.SolicitarAtualizacaoAsync(id), "Atualização solicitada. O motor baixa a lista em até 1 minuto.");

    public string NomeLista(int id) => Listas.FirstOrDefault(l => l.Id == id)?.Nome ?? "—";

    public string? DescreverLocal(string ip)
    {
        var local = EnderecoIp.TentarConverter(ip, out var endereco) ? geolocalizacao.Localizar(endereco) : null;
        return local is null
            ? null
            : string.Join(" · ", new[]
            {
                local.Cidade,
                local.Asn is { } asn ? $"AS{asn} {local.Organizacao}".Trim() : null
            }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
    }

    public string? PaisDoIp(string ip) =>
        EnderecoIp.TentarConverter(ip, out var endereco) ? geolocalizacao.Localizar(endereco)?.PaisCodigo : null;
}
