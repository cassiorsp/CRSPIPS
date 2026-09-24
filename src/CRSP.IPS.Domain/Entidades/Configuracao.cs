using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Domain.Entidades;

/// <summary>Parametros do motor, editados pela tela de Configuracoes. Registro unico (Id = 1).</summary>
public class Configuracao
{
    public const int IdUnico = 1;

    public int Id { get; private set; } = IdUnico;
    public bool ModoSimulacao { get; private set; } = true;
    public string TemposBloqueio { get; private set; } = "1h, 24h, 7d, 30d";
    public int JanelaReincidenciaDias { get; private set; } = 30;
    public bool MonitorarLogIis { get; private set; } = true;
    public string CaminhoLogIis { get; private set; } = @"C:\inetpub\logs\LogFiles";
    public bool MonitorarHttpErr { get; private set; } = true;
    public string CaminhoHttpErr { get; private set; } = @"C:\Windows\System32\LogFiles\HTTPERR";
    public bool MonitorarEventosWindows { get; private set; } = true;
    public int RetencaoEventosDias { get; private set; } = 30;
    public ModoPoliticaPaises ModoPaises { get; private set; } = ModoPoliticaPaises.Desativada;
    public AplicacaoPoliticaPaises AplicacaoPaises { get; private set; } = AplicacaoPoliticaPaises.ReativaSuspeitos;
    public string PaisesPolitica { get; private set; } = string.Empty;
    public string PortasPolitica { get; private set; } = "3389";
    public DateTime AtualizadaEm { get; private set; }
    public string? AtualizadaPor { get; private set; }

    /// <summary>Ultima versao do catalogo de regras padrao ja aplicada. Cada versao e aplicada uma unica vez.</summary>
    public int VersaoRegrasPadrao { get; private set; }

    public void MarcarRegrasPadraoAplicadas(int versao)
    {
        if (versao > VersaoRegrasPadrao)
            VersaoRegrasPadrao = versao;
    }

    public IReadOnlyList<TimeSpan> ObterTemposBloqueio() =>
        DuracaoTexto.TentarConverterLista(TemposBloqueio, out var tempos) ? tempos : [TimeSpan.FromHours(24)];

    public IReadOnlySet<string> ObterPaises() =>
        PaisesPolitica.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<int> ObterPortas() =>
        PortasPolitica.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, out var porta) ? porta : 0)
            .Where(p => p is > 0 and <= 65535)
            .Distinct()
            .Order()
            .ToList();

    public void AlterarMotor(
        bool modoSimulacao,
        string temposBloqueio,
        int janelaReincidenciaDias,
        bool monitorarLogIis,
        string caminhoLogIis,
        bool monitorarHttpErr,
        string caminhoHttpErr,
        bool monitorarEventosWindows,
        int retencaoEventosDias,
        DateTime agoraUtc,
        string usuario)
    {
        if (!DuracaoTexto.TentarConverterLista(temposBloqueio, out var tempos))
            throw new ArgumentException("Tempos de bloqueio inválidos. Use o formato: 1h, 24h, 7d.");
        if (janelaReincidenciaDias is < 1 or > 365)
            throw new ArgumentException("A janela de reincidência deve estar entre 1 e 365 dias.");
        if (retencaoEventosDias is < 1 or > 365)
            throw new ArgumentException("A retenção deve estar entre 1 e 365 dias.");

        ModoSimulacao = modoSimulacao;
        TemposBloqueio = DuracaoTexto.FormatarLista(tempos);
        JanelaReincidenciaDias = janelaReincidenciaDias;
        MonitorarLogIis = monitorarLogIis;
        CaminhoLogIis = caminhoLogIis.Trim();
        MonitorarHttpErr = monitorarHttpErr;
        CaminhoHttpErr = caminhoHttpErr.Trim();
        MonitorarEventosWindows = monitorarEventosWindows;
        RetencaoEventosDias = retencaoEventosDias;
        RegistrarAlteracao(agoraUtc, usuario);
    }

    public void AlterarPoliticaPaises(
        ModoPoliticaPaises modo,
        AplicacaoPoliticaPaises aplicacao,
        IEnumerable<string> paises,
        IEnumerable<int> portas,
        DateTime agoraUtc,
        string usuario)
    {
        var listaPaises = paises
            .Where(p => p.Length == 2)
            .Select(p => p.ToUpperInvariant())
            .Distinct()
            .Order()
            .ToList();
        var listaPortas = portas.Where(p => p is > 0 and <= 65535).Distinct().Order().ToList();

        if (modo != ModoPoliticaPaises.Desativada && listaPaises.Count == 0)
            throw new ArgumentException("Selecione ao menos um país.");
        if (modo != ModoPoliticaPaises.Desativada && aplicacao == AplicacaoPoliticaPaises.FirewallPorPortas && listaPortas.Count == 0)
            throw new ArgumentException("Informe ao menos uma porta.");

        ModoPaises = modo;
        AplicacaoPaises = aplicacao;
        PaisesPolitica = string.Join(",", listaPaises);
        PortasPolitica = string.Join(",", listaPortas);
        RegistrarAlteracao(agoraUtc, usuario);
    }

    /// <summary>Indica se o pais pode acessar segundo a politica. Pais desconhecido e sempre permitido.</summary>
    public bool PaisPermitido(string? paisCodigo)
    {
        if (ModoPaises == ModoPoliticaPaises.Desativada || string.IsNullOrWhiteSpace(paisCodigo))
            return true;

        var listado = ObterPaises().Contains(paisCodigo);
        return ModoPaises == ModoPoliticaPaises.PermitirSomenteListados ? listado : !listado;
    }

    private void RegistrarAlteracao(DateTime agoraUtc, string usuario)
    {
        AtualizadaEm = agoraUtc;
        AtualizadaPor = usuario;
    }
}
