namespace CRSP.IPS.Domain.Enums;

public enum StatusBloqueio
{
    Ativo = 1,
    Expirado = 2,
    Liberado = 3
}

public enum OrigemBloqueio
{
    Automatico = 1,
    Manual = 2,
    Pais = 3
}

public enum TipoFonte
{
    LogIis = 1,
    HttpErr = 2,
    EventoWindows = 3
}

public enum TipoCriterio
{
    /// <summary>Padrao = codigos HTTP separados por virgula (ex: "404,403").</summary>
    CodigoStatus = 1,

    /// <summary>Padrao = expressao regular aplicada na URL (caminho + query).</summary>
    PadraoUrl = 2,

    /// <summary>Padrao = expressao regular aplicada no motivo do HTTPERR (s-reason).</summary>
    MotivoHttpErr = 3,

    /// <summary>Padrao = IDs de evento do Windows separados por virgula (ex: "4625,140").</summary>
    IdEventoWindows = 4
}

public enum TipoLista
{
    Branca = 1,
    Negra = 2
}

public enum FormatoListaExterna
{
    /// <summary>Um IP, CIDR ou intervalo por linha (o primeiro campo da linha). Comentarios com # ou ;.</summary>
    TextoSimples = 1,

    /// <summary>Spamhaus DROP: um objeto JSON por linha com "cidr" e "sblid".</summary>
    SpamhausJson = 2,

    /// <summary>DShield block.txt: colunas separadas por tabulacao com IP inicial e final.</summary>
    DShield = 3
}

public enum ModoListaExterna
{
    Desativada = 0,

    /// <summary>Baixa e registra coincidencias com o trafego real, mas nao aplica no firewall.</summary>
    Avaliacao = 1,

    /// <summary>Baixa e aplica no firewall (exceto em modo simulacao).</summary>
    Ativa = 2
}

public enum ModoPoliticaPaises
{
    Desativada = 0,
    BloquearListados = 1,
    PermitirSomenteListados = 2
}

public enum AplicacaoPoliticaPaises
{
    /// <summary>Bloqueia o IP no primeiro evento de qualquer tipo (inclusive acesso normal a sites) vindo de pais nao permitido.</summary>
    Reativa = 1,

    /// <summary>Restringe as portas informadas direto no firewall usando as faixas de IP dos paises.</summary>
    FirewallPorPortas = 2,

    /// <summary>
    /// Bloqueia na hora o IP de pais nao permitido que gerar um evento suspeito (casar com qualquer regra ativa),
    /// sem esperar o limite da regra. Visitantes normais, buscadores e integracoes nao sao afetados.
    /// </summary>
    ReativaSuspeitos = 3
}
