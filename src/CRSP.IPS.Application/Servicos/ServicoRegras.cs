using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;

namespace CRSP.IPS.Application.Servicos;

public sealed class ServicoRegras(
    IRepositorioRegras regras,
    ServicoAuditoria auditoria,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    public Task<IReadOnlyList<RegraDeteccao>> ListarAsync(CancellationToken ct = default) => regras.ListarAsync(ct);

    public Task<RegraDeteccao?> ObterAsync(int id, CancellationToken ct = default) => regras.ObterPorIdAsync(id, ct);

    public async Task<Resultado> SalvarAsync(int? id, DadosRegra dados, CancellationToken ct = default)
    {
        var erro = RegraDeteccao.Validar(dados.Nome, dados.Fonte, dados.Criterio, dados.Padrao, dados.LimiteOcorrencias, dados.JanelaSegundos);
        if (erro is not null)
            return Resultado.Falha(erro);

        var agora = relogio.GetUtcNow().UtcDateTime;
        if (id is null)
        {
            regras.Adicionar(RegraDeteccao.Criar(dados.Nome, dados.Descricao, dados.Fonte, dados.Criterio, dados.Padrao,
                dados.LimiteOcorrencias, dados.JanelaSegundos, dados.Ativa, agora));
            auditoria.Registrar("Regra criada", dados.Nome);
        }
        else
        {
            var regra = await regras.ObterPorIdAsync(id.Value, ct);
            if (regra is null)
                return Resultado.Falha("Regra não encontrada.");
            regra.Alterar(dados.Nome, dados.Descricao, dados.Fonte, dados.Criterio, dados.Padrao,
                dados.LimiteOcorrencias, dados.JanelaSegundos, dados.Ativa, agora);
            auditoria.Registrar("Regra alterada", dados.Nome);
        }

        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> AlternarAtivaAsync(int id, CancellationToken ct = default)
    {
        var regra = await regras.ObterPorIdAsync(id, ct);
        if (regra is null)
            return Resultado.Falha("Regra não encontrada.");

        regra.DefinirAtiva(!regra.Ativa, relogio.GetUtcNow().UtcDateTime);
        auditoria.Registrar(regra.Ativa ? "Regra ativada" : "Regra desativada", regra.Nome);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ExcluirAsync(int id, CancellationToken ct = default)
    {
        var regra = await regras.ObterPorIdAsync(id, ct);
        if (regra is null)
            return Resultado.Falha("Regra não encontrada.");
        if (await regras.PossuiBloqueiosAsync(id, ct))
            return Resultado.Falha("A regra possui bloqueios no histórico. Desative-a em vez de excluir.");

        regras.Remover(regra);
        auditoria.Registrar("Regra excluída", regra.Nome);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
