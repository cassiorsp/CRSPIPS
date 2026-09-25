using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;

namespace CRSP.IPS.Application.Servicos;

/// <summary>
/// Operacoes do painel sobre listas externas. O painel apenas liga/desliga e pede atualizacao;
/// o download e a aplicacao no firewall sao feitos pelo Worker.
/// </summary>
public sealed class ServicoListasExternas(
    IRepositorioListasExternas listas,
    IRepositorioBloqueios bloqueios,
    IRepositorioConfiguracao configuracoes,
    ServicoAuditoria auditoria,
    IUnidadeDeTrabalho unidadeDeTrabalho)
{
    public Task<IReadOnlyList<ListaExterna>> ListarAsync(CancellationToken ct = default) => listas.ListarAsync(ct);

    public Task<IReadOnlyList<CoincidenciaListaExterna>> ListarCoincidenciasAsync(CancellationToken ct = default) =>
        listas.ListarCoincidenciasRecentesAsync(100, ct);

    /// <summary>Bloqueios ativos (reais ou simulados) por IP, para mostrar se um IP das coincidencias ja esta bloqueado.</summary>
    public async Task<IReadOnlyDictionary<string, Bloqueio>> ListarBloqueiosAtivosPorIpAsync(CancellationToken ct = default) =>
        (await bloqueios.ListarAtivosAsync(ct))
            .GroupBy(b => b.Ip)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Simulado).First());

    public async Task<bool> EstaEmSimulacaoAsync(CancellationToken ct = default) => (await configuracoes.ObterAsync(ct)).ModoSimulacao;

    public async Task<Resultado> DefinirModoAsync(int id, ModoListaExterna modo, CancellationToken ct = default)
    {
        var lista = await listas.ObterPorIdAsync(id, ct);
        if (lista is null)
            return Resultado.Falha("Lista não encontrada.");
        if (!Enum.IsDefined(modo))
            return Resultado.Falha("Modo inválido.");

        lista.DefinirModo(modo);
        auditoria.Registrar("Modo da lista externa alterado", lista.Nome, modo.ToString());
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public const int LimiteMaximoEntradas = 200_000;

    public async Task<Resultado> CriarAsync(DadosListaExterna dados, CancellationToken ct = default)
    {
        var nome = dados.Nome?.Trim() ?? string.Empty;
        if (nome.Length is 0 or > 100)
            return Resultado.Falha("Informe um nome de até 100 caracteres.");
        if (await listas.ExisteNomeAsync(nome, ct))
            return Resultado.Falha("Já existe uma lista com este nome.");
        if (dados.LimiteEntradas is < 1 or > LimiteMaximoEntradas)
            return Resultado.Falha("O limite de entradas deve estar entre 1 e 200.000.");
        if (!Enum.IsDefined(dados.Formato) || !Enum.IsDefined(dados.Modo))
            return Resultado.Falha("Formato ou modo inválido.");

        ListaExterna lista;
        try
        {
            lista = ListaExterna.Criar(nome, string.IsNullOrWhiteSpace(dados.Descricao) ? null : dados.Descricao.Trim(),
                dados.Urls, dados.Formato, dados.IntervaloHoras, dados.LimiteEntradas, dados.Modo, personalizada: true);
        }
        catch (ArgumentException ex)
        {
            return Resultado.Falha(ex.Message);
        }

        listas.Adicionar(lista);
        auditoria.Registrar("Lista externa criada", lista.Nome, lista.Urls.Replace('\n', ' '));
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirAsync(int id, CancellationToken ct = default)
    {
        var lista = await listas.ObterPorIdAsync(id, ct);
        if (lista is null)
            return Resultado.Falha("Lista não encontrada.");
        if (!lista.Personalizada)
            return Resultado.Falha("Listas do catálogo não podem ser excluídas. Use o modo Desativada.");

        listas.Remover(lista);
        auditoria.Registrar("Lista externa excluída", lista.Nome);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> SolicitarAtualizacaoAsync(int id, CancellationToken ct = default)
    {
        var lista = await listas.ObterPorIdAsync(id, ct);
        if (lista is null)
            return Resultado.Falha("Lista não encontrada.");
        if (!lista.EstaEmUso)
            return Resultado.Falha("Coloque a lista em avaliação ou ative-a antes de atualizar.");

        lista.SolicitarAtualizacao();
        auditoria.Registrar("Atualização de lista externa solicitada", lista.Nome);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
