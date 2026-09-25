using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;

namespace CRSP.IPS.Application.Servicos;

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

    public const int LimiteLinhasImportacao = 5000;

    public async Task<Resultado> AdicionarAsync(TipoLista tipo, string faixaTexto, string? descricao, CancellationToken ct = default)
    {
        var erro = await IncluirAsync(tipo, faixaTexto, descricao, new Lazy<Task<ConjuntoProtecao>>(() => protecao.ObterAsync(ct)), ct);
        if (erro is not null)
            return Resultado.Falha(erro);

        auditoria.Registrar(tipo == TipoLista.Branca ? "Lista branca: inclusão" : "Lista negra: inclusão", faixaTexto.Trim(), descricao);
        await unidadeDeTrabalho.SalvarAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Importacao em lote (CSV). Cada linha passa pelas mesmas validacoes da inclusao manual; linhas invalidas sao
    /// relatadas e as validas sao gravadas juntas, com um unico registro de auditoria.
    /// </summary>
    public async Task<Resultado<ResultadoImportacao>> ImportarAsync(TipoLista tipo, IReadOnlyList<LinhaImportacao> linhas, CancellationToken ct = default)
    {
        if (linhas.Count == 0)
            return Resultado<ResultadoImportacao>.Falha("O arquivo não tem nenhuma linha com faixa.");
        if (linhas.Count > LimiteLinhasImportacao)
            return Resultado<ResultadoImportacao>.Falha("O arquivo passa do limite de 5.000 linhas. Divida em arquivos menores.");

        var conjuntoProtecao = new Lazy<Task<ConjuntoProtecao>>(() => protecao.ObterAsync(ct));
        var noArquivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var erros = new List<ErroImportacao>();
        var adicionadas = 0;
        var duplicadas = 0;

        foreach (var linha in linhas)
        {
            var faixa = linha.Faixa.Trim();
            if (!noArquivo.Add(faixa))
            {
                duplicadas++;
                continue;
            }

            var descricao = linha.Descricao is { Length: > 300 } longa ? longa[..300] : linha.Descricao;
            var erro = await IncluirAsync(tipo, faixa, descricao, conjuntoProtecao, ct);
            if (erro == FaixaJaCadastrada)
                duplicadas++;
            else if (erro is not null)
                erros.Add(new ErroImportacao(linha.Numero, faixa, erro));
            else
                adicionadas++;
        }

        if (adicionadas > 0)
        {
            auditoria.Registrar(tipo == TipoLista.Branca ? "Lista branca: importação CSV" : "Lista negra: importação CSV",
                $"{adicionadas} faixa(s)", $"Duplicadas: {duplicadas} · Com erro: {erros.Count}");
            await unidadeDeTrabalho.SalvarAsync(ct);
        }

        return Resultado<ResultadoImportacao>.Ok(new ResultadoImportacao(adicionadas, duplicadas, erros));
    }

    private const string FaixaJaCadastrada = "Esta faixa já está cadastrada.";

    /// <summary>Valida e inclui a faixa (sem salvar). Retorna a mensagem de erro ou nulo.</summary>
    private async Task<string?> IncluirAsync(TipoLista tipo, string faixaTexto, string? descricao, Lazy<Task<ConjuntoProtecao>> conjuntoProtecao, CancellationToken ct)
    {
        if (!FaixaIp.TentarConverter(faixaTexto, out var faixa) || faixa is null)
            return "Informe um IP, CIDR (ex: 10.0.0.0/8) ou intervalo (ex: 10.0.0.1-10.0.0.9) válido.";

        var texto = faixaTexto.Trim();
        if (await listas.ExisteAsync(tipo, texto, ct))
            return FaixaJaCadastrada;

        var agora = relogio.GetUtcNow().UtcDateTime;

        if (tipo == TipoLista.Negra)
        {
            if (contexto.Ip is not null && EnderecoIp.TentarConverter(contexto.Ip, out var ipUsuario) && faixa.Contem(ipUsuario))
                return "A faixa contém o seu próprio IP.";
            if ((await conjuntoProtecao.Value).Sobrepoe(faixa))
                return "A faixa sobrepõe endereços protegidos (lista branca, servidor ou administrador).";
        }
        else
        {
            var ativos = await bloqueios.ListarAtivosNaFaixaAsync(faixa.Inicio.ObterChaveOrdenavel(), faixa.Fim.ObterChaveOrdenavel(), ct);
            foreach (var bloqueio in ativos)
                bloqueio.Liberar(agora, contexto.Nome, "Adicionado à lista branca");
        }

        listas.Adicionar(EntradaLista.Criar(tipo, faixa, texto, descricao, sistema: false, agora, contexto.Nome));
        return null;
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
