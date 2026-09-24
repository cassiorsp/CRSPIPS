using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Servicos;
using CRSP.IPS.Domain.Entidades;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Application.Motor;

/// <summary>
/// Baixa as listas externas vencidas, valida, remove o que colide com enderecos protegidos e troca as entradas.
/// Qualquer falha (download, conteudo vazio, acima do limite) mantem as entradas anteriores.
/// </summary>
public sealed class ServicoAtualizacaoListasExternas(
    IRepositorioListasExternas listas,
    IBaixadorListasExternas baixador,
    ServicoProtecao protecao,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio,
    ILogger<ServicoAtualizacaoListasExternas> logger)
{
    public async Task AtualizarPendentesAsync(CancellationToken ct = default)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var pendentes = (await listas.ListarAsync(ct)).Where(l => l.DeveAtualizar(agora)).ToList();
        if (pendentes.Count == 0)
            return;

        var conjuntoProtecao = await protecao.ObterAsync(ct);
        foreach (var lista in pendentes)
        {
            try
            {
                await AtualizarAsync(lista, conjuntoProtecao, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                lista.RegistrarFalha(relogio.GetUtcNow().UtcDateTime, "Falha ao baixar a lista. As entradas anteriores continuam valendo.");
                logger.LogWarning(ex, "Falha ao atualizar a lista externa {Lista}", lista.Nome);
            }

            await unidadeDeTrabalho.SalvarAsync(ct);
        }
    }

    private async Task AtualizarAsync(ListaExterna lista, ConjuntoProtecao conjuntoProtecao, CancellationToken ct)
    {
        var conteudos = await baixador.BaixarAsync(lista.ObterUrls(), ct);
        var interpretadas = conteudos.SelectMany(c => InterpretadorListaExterna.Interpretar(c, lista.Formato)).ToList();
        var agora = relogio.GetUtcNow().UtcDateTime;

        if (interpretadas.Count == 0)
        {
            lista.RegistrarFalha(agora, "A lista baixada não tem nenhuma entrada válida. As entradas anteriores continuam valendo.");
            logger.LogWarning("Lista externa {Lista} sem entradas validas; mantida a versao anterior", lista.Nome);
            return;
        }

        if (interpretadas.Count > lista.LimiteEntradas)
        {
            lista.RegistrarFalha(agora, "A lista baixada passou do limite de entradas. As entradas anteriores continuam valendo.");
            logger.LogWarning(
                "Lista externa {Lista} com {Quantidade} entradas, acima do limite {Limite}; mantida a versao anterior",
                lista.Nome, interpretadas.Count, lista.LimiteEntradas);
            return;
        }

        var aceitas = interpretadas.Where(e => !conjuntoProtecao.Sobrepoe(e.Faixa)).ToList();
        var removidas = interpretadas.Count - aceitas.Count;

        await listas.SubstituirEntradasAsync(lista.Id, aceitas.Select(e => EntradaListaExterna.Criar(lista.Id, e.Faixa, e.Referencia)).ToList(), ct);
        lista.RegistrarSucesso(agora, aceitas.Count, removidas);

        logger.LogInformation(
            "Lista externa {Lista} atualizada: {Quantidade} entradas, {Removidas} removidas pela protecao",
            lista.Nome, aceitas.Count, removidas);
    }
}
