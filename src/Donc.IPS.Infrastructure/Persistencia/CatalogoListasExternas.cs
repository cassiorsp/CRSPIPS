using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;

namespace Donc.IPS.Infrastructure.Persistencia;

/// <summary>
/// Listas publicas de IPs maliciosos entregues com o CRSPIPS. Ativas: as de baixissimo falso positivo
/// (redes criminosas e consenso de sensores). Desativadas: listas de IPs individuais, que podem incluir IPs
/// dinamicos/CGNAT reatribuidos a usuarios legitimos; avalie as coincidencias em simulacao antes de ligar.
/// </summary>
internal static class CatalogoListasExternas
{
    public static IEnumerable<ListaExterna> Listar()
    {
        yield return ListaExterna.Criar("Spamhaus DROP",
            "Redes sequestradas ou operadas por criminosos, sem nenhum uso legítimo. Mantida manualmente pela Spamhaus.",
            ["https://www.spamhaus.org/drop/drop_v4.json", "https://www.spamhaus.org/drop/drop_v6.json"],
            FormatoListaExterna.SpamhausJson, intervaloHoras: 24, limiteEntradas: 10_000, ModoListaExterna.Ativa);

        yield return ListaExterna.Criar("DShield Top 20",
            "As 20 redes /24 que mais atacaram os sensores do SANS Internet Storm Center nos últimos dias.",
            ["https://feeds.dshield.org/block.txt"],
            FormatoListaExterna.DShield, intervaloHoras: 24, limiteEntradas: 100, ModoListaExterna.Ativa);

        yield return ListaExterna.Criar("IPsum nível 3+",
            "IPs presentes em 3 ou mais listas públicas de ataque. Avalie as coincidências em simulação antes de ativar.",
            ["https://raw.githubusercontent.com/stamparm/ipsum/master/levels/3.txt"],
            FormatoListaExterna.TextoSimples, intervaloHoras: 24, limiteEntradas: 100_000, ModoListaExterna.Desativada);

        yield return ListaExterna.Criar("CINS Army",
            "IPs com pior reputação na rede de sensores da Sentinel IPS. Avalie as coincidências em simulação antes de ativar.",
            ["https://cinsscore.com/list/ci-badguys.txt"],
            FormatoListaExterna.TextoSimples, intervaloHoras: 24, limiteEntradas: 50_000, ModoListaExterna.Desativada);

        yield return ListaExterna.Criar("blocklist.de",
            "IPs reportados atacando SSH, e-mail, FTP e web nas últimas 48 horas. Mais ruído que as demais.",
            ["https://lists.blocklist.de/lists/all.txt"],
            FormatoListaExterna.TextoSimples, intervaloHoras: 24, limiteEntradas: 100_000, ModoListaExterna.Desativada);
    }
}
