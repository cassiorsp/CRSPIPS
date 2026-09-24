using CRSP.IPS.Application.Abstracoes;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Infra;

/// <summary>Selos no topo: Worker online/offline e modo simulacao.</summary>
public sealed class StatusMotorViewComponent(
    IRepositorioConfiguracao configuracoes,
    IRepositorioStatusWorker statusWorker,
    TimeProvider relogio) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var configuracao = await configuracoes.ObterAsync();
        var status = await statusWorker.ObterAsync();
        var online = status?.EstaOnline(relogio.GetUtcNow().UtcDateTime) ?? false;
        return View((configuracao.ModoSimulacao, online));
    }
}
