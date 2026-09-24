using System.Text.RegularExpressions;
using Donc.IPS.Domain.Enums;

namespace Donc.IPS.Domain.Entidades;

/// <summary>
/// Regra de deteccao no modelo "jail": fonte + criterio + limite de ocorrencias dentro de uma janela de tempo.
/// </summary>
public class RegraDeteccao
{
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public TipoFonte Fonte { get; private set; }
    public TipoCriterio Criterio { get; private set; }
    public string Padrao { get; private set; } = string.Empty;
    public int LimiteOcorrencias { get; private set; }
    public int JanelaSegundos { get; private set; }
    public bool Ativa { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime AtualizadaEm { get; private set; }

    protected RegraDeteccao() { }

    public static RegraDeteccao Criar(
        string nome,
        string? descricao,
        TipoFonte fonte,
        TipoCriterio criterio,
        string padrao,
        int limiteOcorrencias,
        int janelaSegundos,
        bool ativa,
        DateTime agoraUtc)
    {
        var regra = new RegraDeteccao { CriadaEm = agoraUtc };
        regra.Alterar(nome, descricao, fonte, criterio, padrao, limiteOcorrencias, janelaSegundos, ativa, agoraUtc);
        return regra;
    }

    public void Alterar(
        string nome,
        string? descricao,
        TipoFonte fonte,
        TipoCriterio criterio,
        string padrao,
        int limiteOcorrencias,
        int janelaSegundos,
        bool ativa,
        DateTime agoraUtc)
    {
        var erro = Validar(nome, fonte, criterio, padrao, limiteOcorrencias, janelaSegundos);
        if (erro is not null)
            throw new ArgumentException(erro);

        Nome = nome.Trim();
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Fonte = fonte;
        Criterio = criterio;
        Padrao = padrao.Trim();
        LimiteOcorrencias = limiteOcorrencias;
        JanelaSegundos = janelaSegundos;
        Ativa = ativa;
        AtualizadaEm = agoraUtc;
    }

    public void DefinirAtiva(bool ativa, DateTime agoraUtc)
    {
        Ativa = ativa;
        AtualizadaEm = agoraUtc;
    }

    public TimeSpan Janela => TimeSpan.FromSeconds(JanelaSegundos);

    /// <summary>Retorna a chave da mensagem de erro ou null quando valida.</summary>
    public static string? Validar(string? nome, TipoFonte fonte, TipoCriterio criterio, string? padrao, int limiteOcorrencias, int janelaSegundos)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "Informe o nome da regra.";
        if (string.IsNullOrWhiteSpace(padrao))
            return "Informe o padrão da regra.";
        if (limiteOcorrencias < 1)
            return "O limite de ocorrências deve ser maior que zero.";
        if (janelaSegundos < 1 || janelaSegundos > 86400)
            return "A janela deve estar entre 1 segundo e 24 horas.";
        if (!CriterioCompativelComFonte(fonte, criterio))
            return "O critério não é compatível com a fonte selecionada.";

        switch (criterio)
        {
            case TipoCriterio.CodigoStatus:
            case TipoCriterio.IdEventoWindows:
                if (padrao.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(p => !int.TryParse(p, out _)))
                    return "Informe números separados por vírgula.";
                break;
            case TipoCriterio.PadraoUrl:
            case TipoCriterio.MotivoHttpErr:
                try
                {
                    _ = new Regex(padrao, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException)
                {
                    return "Expressão regular inválida.";
                }
                break;
        }

        return null;
    }

    public static bool CriterioCompativelComFonte(TipoFonte fonte, TipoCriterio criterio) => (fonte, criterio) switch
    {
        (TipoFonte.LogIis, TipoCriterio.CodigoStatus or TipoCriterio.PadraoUrl) => true,
        (TipoFonte.HttpErr, TipoCriterio.CodigoStatus or TipoCriterio.PadraoUrl or TipoCriterio.MotivoHttpErr) => true,
        (TipoFonte.EventoWindows, TipoCriterio.IdEventoWindows) => true,
        _ => false
    };
}
