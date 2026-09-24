using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Application.Modelos;
using Donc.IPS.Domain.Entidades;
using Donc.IPS.Domain.Enums;
using Donc.IPS.Domain.ObjetosValor;

namespace Donc.IPS.Application.Servicos;

public sealed class ServicoListas(
    IRepositorioListas listas,
    IRepositorioBloqueios bloqueios,
    ServicoProtecao protecao,
    ServicoAuditoria auditoria,
    IContextoUsuario contexto,
    IUnidadeDeTrabalho unidadeDeTrabalho,
    TimeProvider relogio)
{
    public Task<IReadOnlyList<EntradaLista>> ListarAsync(TipoLista tipo, CancellationToken ct = default) =>
        listas.ListarAsync(tipo, ct);

    public async Task<Resultado> AdicionarAsync(TipoLista tipo, string faixaTexto, string? descricao, CancellationToken ct = default)
    {
        if (!FaixaIp.TentarConverter(faixaTexto, out var faixa) || faixa is null)
            return Resultado.Falha("Informe um IP, CIDR (ex: 10.0.0.0/8) ou intervalo (ex: 10.0.0.1-10.0.0.9) válido.");

        var texto = faixaTexto.Trim();
        if (await listas.ExisteAsync(tipo, texto, ct))
            return Resultado.Falha("Esta faixa já está cadastrada.");

        var agora = relogio.GetUtcNow().UtcDateTime;

        if (tipo == TipoLista.Negra)
        {
            if (contexto.Ip is not null && EnderecoIp.TentarConverter(contexto.Ip, out var ipUsuario) && faixa.Contem(ipUsuario))
                return Resultado.Falha("A faixa contém o seu próprio IP.");
            if ((await protecao.ObterAsync(ct)).Sobrepoe(faixa))
                return Resultado.Falha("A faixa sobrepõe endereços protegidos (lista branca, servidor ou administrador).");
        }
        else
        {
            var ativos = await bloqueios.ListarAtivosNaFaixaAsync(faixa.Inicio.ObterChaveOrdenavel(), faixa.Fim.ObterChaveOrdenavel(), ct);
            foreach (var bloqueio in ativos)
                bloqueio.Liberar(agora, contexto.Nome, "Adicionado à lista branca");
        }

        listas.Adicionar(EntradaLista.Criar(tipo, faixa, texto, descricao, sistema: false, agora, contexto.Nome));
        auditoria.Registrar(tipo == TipoLista.Branca ? "Lista branca: inclusão" : "Lista negra: inclusão", texto, descricao);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> RemoverAsync(int id, CancellationToken ct = default)
    {
        var entrada = await listas.ObterPorIdAsync(id, ct);
        if (entrada is null)
            return Resultado.Falha("Registro não encontrado.");
        if (entrada.Sistema)
            return Resultado.Falha("Entradas do sistema não podem ser removidas.");

        listas.Remover(entrada);
        auditoria.Registrar(entrada.Tipo == TipoLista.Branca ? "Lista branca: remoção" : "Lista negra: remoção", entrada.Faixa, entrada.Descricao);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }
}
