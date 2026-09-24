using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Preenche pais, cidade e provedor de bloqueios e eventos gravados enquanto a base GeoIP ainda nao existia
/// (ex.: primeiros ataques antes de cadastrar a MaxMind). IPs sem localizacao na base continuam vazios.
/// </summary>
public sealed class ServicoCompletarLocalizacao(
    IRepositorioBloqueios bloqueios,
    IRepositorioEventos eventos,
    IServicoGeolocalizacao geolocalizacao,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoCompletarLocalizacao> logger)
{
    private static readonly TimeSpan JanelaRetroativa = TimeSpan.FromDays(30);
    private const int LimitePorExecucao = 2000;

    public async Task ExecutarAsync(CancellationToken ct = default)
    {
        if (!geolocalizacao.ObterStatus().CidadeEncontrada)
            return;

        var desde = relogio.GetUtcNow().UtcDateTime - JanelaRetroativa;
        var cache = new Dictionary<string, LocalizacaoIp?>();

        LocalizacaoIp? Localizar(string ip)
        {
            if (!cache.TryGetValue(ip, out var local))
            {
                local = EnderecoIp.TentarConverter(ip, out var endereco) ? geolocalizacao.Localizar(endereco) : null;
                cache[ip] = local;
            }

            return local;
        }

        var bloqueiosCompletados = 0;
        foreach (var bloqueio in await bloqueios.ListarSemLocalizacaoDesdeAsync(desde, LimitePorExecucao, ct))
        {
            if (Localizar(bloqueio.Ip) is { } local)
            {
                bloqueio.DefinirLocalizacao(local);
                bloqueiosCompletados++;
            }
        }

        var eventosCompletados = 0;
        foreach (var evento in await eventos.ListarSemPaisDesdeAsync(desde, LimitePorExecucao, ct))
        {
            if (Localizar(evento.Ip)?.PaisCodigo is { } pais)
            {
                evento.DefinirPais(pais);
                eventosCompletados++;
            }
        }

        if (bloqueiosCompletados == 0 && eventosCompletados == 0)
            return;

        await unidadeDeTrabalho.SalvarAsync(ct);
        logger.LogInformation(
            "Localizacao completada retroativamente: {Bloqueios} bloqueios e {Eventos} eventos",
            bloqueiosCompletados, eventosCompletados);
    }
}
