using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;

namespace Donc.IPS.Application.Servicos;

public sealed class ServicoAuditoria(IRepositorioAuditoria auditoria, IContextoUsuario contexto, TimeProvider relogio)
{
    /// <summary>Adiciona o registro na unidade de trabalho corrente. Quem chama e responsavel por salvar.</summary>
    public void Registrar(string acao, string alvo, string? detalhe = null) =>
        auditoria.Adicionar(RegistroAuditoria.Criar(relogio.GetUtcNow().UtcDateTime, contexto.Nome, contexto.Ip, acao, alvo, detalhe));

    public Task<Pagina<RegistroAuditoria>> PesquisarAsync(string? texto, int pagina, CancellationToken ct = default) =>
        auditoria.PesquisarAsync(texto, Math.Max(1, pagina), 50, ct);
}
