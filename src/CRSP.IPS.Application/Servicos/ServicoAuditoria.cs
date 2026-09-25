using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;

namespace CRSP.IPS.Application.Servicos;

public sealed class ServicoAuditoria(IRepositorioAuditoria auditoria, IContextoUsuario contexto, TimeProvider relogio)
{
    /// <summary>Adiciona o registro na unidade de trabalho corrente. Quem chama e responsavel por salvar.</summary>
    public void Registrar(string acao, string alvo, string? detalhe = null) =>
        auditoria.Adicionar(RegistroAuditoria.Criar(relogio.GetUtcNow().UtcDateTime, contexto.Nome, contexto.Ip, acao, alvo, detalhe));

    public Task<Fatia<RegistroAuditoria>> PesquisarAsync(string? texto, int pular = 0, int quantidade = 50, CancellationToken ct = default) =>
        auditoria.PesquisarAsync(texto, Math.Max(0, pular), Math.Clamp(quantidade, 1, 200), ct);
}
