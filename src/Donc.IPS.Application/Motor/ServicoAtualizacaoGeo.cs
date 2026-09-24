using Donc.IPS.Application.Abstracoes;
using Microsoft.Extensions.Logging;

namespace Donc.IPS.Application.Motor;

/// <summary>
/// Executado periodicamente pelo Worker: baixa as bases GeoLite2 quando o administrador pede
/// ("Atualizar agora") ou, com a atualizacao automatica ligada, a cada 24 horas.
/// </summary>
public sealed class ServicoAtualizacaoGeo(
    IRepositorioConfiguracaoGeoIp configuracoes,
    IAtualizadorBaseGeo atualizador,
    IProtetorSegredos protetor,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoAtualizacaoGeo> logger)
{
    public async Task ExecutarSeNecessarioAsync(CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(ct);
        if (!configuracao.DeveVerificar(relogio.GetUtcNow().UtcDateTime))
            return;

        var forcar = configuracao.AtualizacaoSolicitada;
        var chave = protetor.Desproteger(configuracao.ChaveProtegida!);
        var resultado = chave is null
            ? new Modelos.ResultadoAtualizacaoGeo(false, 0, "Não foi possível ler a chave de licença. Cadastre-a novamente.")
            : await atualizador.AtualizarAsync(configuracao.ContaId!, chave, forcar, ct);

        configuracao.RegistrarVerificacao(relogio.GetUtcNow().UtcDateTime, resultado.Sucesso, resultado.BasesAtualizadas, resultado.Mensagem);
        await unidadeDeTrabalho.SalvarAsync(ct);

        if (resultado.Sucesso)
            logger.LogInformation("GeoIP verificado: {Quantidade} bases atualizadas", resultado.BasesAtualizadas);
        else
            logger.LogWarning("Falha na atualizacao GeoIP: {Mensagem}", resultado.Mensagem);
    }
}
