using System.ComponentModel.DataAnnotations;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Web.Infra;
using Microsoft.AspNetCore.Mvc;

namespace CRSP.IPS.Web.Pages.Configuracoes;

public sealed class IndexModel(ServicoConfiguracao servicoConfiguracao, ServicoGeoIp servicoGeoIp, ServicoHistorico servicoHistorico) : PaginaBase
{
    [BindProperty]
    public DadosFormulario Dados { get; set; } = new();

    public StatusBaseGeo StatusGeo { get; private set; } = null!;

    public ConfiguracaoGeoIp GeoIp { get; private set; } = null!;

    public IReadOnlyList<LeituraFonte> Leituras { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CarregarGeoIpAsync(ct);
        var configuracao = await servicoConfiguracao.ObterAsync(ct);
        Dados = new DadosFormulario
        {
            ModoSimulacao = configuracao.ModoSimulacao,
            TemposBloqueio = configuracao.TemposBloqueio,
            JanelaReincidenciaDias = configuracao.JanelaReincidenciaDias,
            MonitorarLogIis = configuracao.MonitorarLogIis,
            CaminhoLogIis = configuracao.CaminhoLogIis,
            MonitorarHttpErr = configuracao.MonitorarHttpErr,
            CaminhoHttpErr = configuracao.CaminhoHttpErr,
            MonitorarEventosWindows = configuracao.MonitorarEventosWindows,
            RetencaoEventosDias = configuracao.RetencaoEventosDias
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await CarregarGeoIpAsync();
            return Page();
        }

        var resultado = await servicoConfiguracao.SalvarMotorAsync(new DadosConfiguracaoMotor(
            Dados.ModoSimulacao, Dados.TemposBloqueio, Dados.JanelaReincidenciaDias,
            Dados.MonitorarLogIis, Dados.CaminhoLogIis, Dados.MonitorarHttpErr, Dados.CaminhoHttpErr,
            Dados.MonitorarEventosWindows, Dados.RetencaoEventosDias));

        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro!);
            await CarregarGeoIpAsync();
            return Page();
        }

        MensagemSucesso = "Configurações salvas. O motor passa a usar os novos valores no próximo ciclo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSalvarGeoIpAsync(string contaId, string? chaveLicenca, bool atualizacaoAutomatica) =>
        Concluir(await servicoGeoIp.SalvarCredenciaisAsync(contaId, chaveLicenca, atualizacaoAutomatica),
            "Credenciais MaxMind salvas. O motor inicia o download em instantes.");

    public async Task<IActionResult> OnPostAtualizarGeoIpAsync() =>
        Concluir(await servicoGeoIp.SolicitarAtualizacaoAsync(), "Atualização solicitada. O motor inicia o download em instantes.");

    public async Task<IActionResult> OnPostLimparHistoricoAsync(bool manterBloqueiosAtivos, bool confirmado)
    {
        if (!confirmado)
            return Concluir(Resultado.Falha("Marque a confirmação para limpar o histórico."), string.Empty);

        return Concluir(await servicoHistorico.LimparAsync(manterBloqueiosAtivos),
            "Histórico limpo. Os detalhes da limpeza estão na Auditoria.");
    }

    public async Task<IActionResult> OnPostRemoverGeoIpAsync() =>
        Concluir(await servicoGeoIp.RemoverCredenciaisAsync(), "Credenciais MaxMind removidas.");

    private async Task CarregarGeoIpAsync(CancellationToken ct = default)
    {
        StatusGeo = servicoConfiguracao.ObterStatusGeo();
        GeoIp = await servicoGeoIp.ObterAsync(ct);
        Leituras = await servicoConfiguracao.ListarLeiturasAsync(ct);
    }

    public sealed class DadosFormulario
    {
        [Display(Name = "Modo simulação")]
        public bool ModoSimulacao { get; set; }

        [Required(ErrorMessage = "Campo obrigatório.")]
        [Display(Name = "Tempos de bloqueio progressivo")]
        public string TemposBloqueio { get; set; } = string.Empty;

        [Range(1, 365, ErrorMessage = "Valor fora do intervalo permitido.")]
        [Display(Name = "Janela de reincidência (dias)")]
        public int JanelaReincidenciaDias { get; set; }

        [Display(Name = "Monitorar logs do IIS")]
        public bool MonitorarLogIis { get; set; }

        [Required(ErrorMessage = "Campo obrigatório.")]
        [Display(Name = "Pasta de logs do IIS")]
        public string CaminhoLogIis { get; set; } = string.Empty;

        [Display(Name = "Monitorar HTTPERR")]
        public bool MonitorarHttpErr { get; set; }

        [Required(ErrorMessage = "Campo obrigatório.")]
        [Display(Name = "Pasta do HTTPERR")]
        public string CaminhoHttpErr { get; set; } = string.Empty;

        [Display(Name = "Monitorar eventos do Windows (RDP, SQL Server)")]
        public bool MonitorarEventosWindows { get; set; }

        [Range(1, 365, ErrorMessage = "Valor fora do intervalo permitido.")]
        [Display(Name = "Retenção de eventos (dias)")]
        public int RetencaoEventosDias { get; set; }
    }
}
