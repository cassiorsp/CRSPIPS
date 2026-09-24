using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;

namespace CRSP.IPS.Application.Abstracoes;

public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct = default);
}

public interface IRepositorioBloqueios
{
    Task<Bloqueio?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Bloqueio?> ObterAtivoPorIpAsync(string ip, CancellationToken ct = default);
    Task<IReadOnlyList<Bloqueio>> ListarAtivosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Bloqueio>> ListarAtivosNaFaixaAsync(string inicioChave, string fimChave, CancellationToken ct = default);
    Task<IReadOnlyList<Bloqueio>> ListarPorIpAsync(string ip, CancellationToken ct = default);
    Task<IReadOnlyList<Bloqueio>> ListarDesdeAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<int> ContarPorIpDesdeAsync(string ip, DateTime desdeUtc, CancellationToken ct = default);
    Task<Pagina<Bloqueio>> PesquisarAsync(FiltroBloqueios filtro, CancellationToken ct = default);
    Task<IReadOnlyList<Bloqueio>> ListarSemLocalizacaoDesdeAsync(DateTime desdeUtc, int limite, CancellationToken ct = default);

    /// <summary>Apaga o historico de bloqueios. Com manterAtivosReais, preserva os que estao aplicados no firewall.</summary>
    Task<int> RemoverHistoricoAsync(bool manterAtivosReais, CancellationToken ct = default);
    void Adicionar(Bloqueio bloqueio);
}

public interface IRepositorioRegras
{
    Task<IReadOnlyList<RegraDeteccao>> ListarAsync(CancellationToken ct = default);
    Task<RegraDeteccao?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<bool> PossuiBloqueiosAsync(int id, CancellationToken ct = default);
    void Adicionar(RegraDeteccao regra);
    void Remover(RegraDeteccao regra);
}

public interface IRepositorioListas
{
    Task<IReadOnlyList<EntradaLista>> ListarAsync(TipoLista? tipo = null, CancellationToken ct = default);
    Task<EntradaLista?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExisteAsync(TipoLista tipo, string faixa, CancellationToken ct = default);
    void Adicionar(EntradaLista entrada);
    void Remover(EntradaLista entrada);
}

public interface IRepositorioConfiguracao
{
    Task<Configuracao> ObterAsync(CancellationToken ct = default);
}

public interface IRepositorioConfiguracaoGeoIp
{
    Task<ConfiguracaoGeoIp> ObterAsync(CancellationToken ct = default);
}

public interface IRepositorioEventos
{
    void AdicionarVarios(IEnumerable<EventoSeguranca> eventos);
    Task<IReadOnlyList<EventoSeguranca>> ListarRecentesAsync(int quantidade, TipoFonte? fonte, long? aposId, CancellationToken ct = default);
    Task<IReadOnlyList<EventoSeguranca>> ListarPorIpAsync(string ip, int quantidade, CancellationToken ct = default);
    Task<IReadOnlyList<DateTime>> ListarMomentosDesdeAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<IReadOnlyList<ItemRanking>> RankingUrlsDesdeAsync(DateTime desdeUtc, int quantidade, CancellationToken ct = default);

    /// <summary>IPs com mais eventos registrados no periodo. Complemento = codigo do pais.</summary>
    Task<IReadOnlyList<ItemRanking>> RankingIpsDesdeAsync(DateTime desdeUtc, int quantidade, CancellationToken ct = default);
    Task<IReadOnlyList<ItemRanking>> ContarPorFonteDesdeAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<int> ContarPaisesDistintosDesdeAsync(DateTime desdeUtc, CancellationToken ct = default);
    Task<int> RemoverAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default);
    Task<IReadOnlyList<EventoSeguranca>> ListarSemPaisDesdeAsync(DateTime desdeUtc, int limite, CancellationToken ct = default);
    Task<int> RemoverTodosAsync(CancellationToken ct = default);
}

public interface IRepositorioUsuarios
{
    Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default);
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken ct = default);
    Task<bool> ExisteAlgumAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListarIpsDeAcessoDesdeAsync(DateTime desdeUtc, CancellationToken ct = default);
    void Adicionar(Usuario usuario);
}

public interface IRepositorioAuditoria
{
    void Adicionar(RegistroAuditoria registro);
    Task<Pagina<RegistroAuditoria>> PesquisarAsync(string? texto, int pagina, int tamanhoPagina, CancellationToken ct = default);
}

public interface IRepositorioPosicoesLeitura
{
    Task<PosicaoLeitura?> ObterAsync(string chave, CancellationToken ct = default);
    Task<IReadOnlyList<PosicaoLeitura>> ListarAsync(CancellationToken ct = default);
    void Adicionar(PosicaoLeitura posicao);
}

public interface IRepositorioListasExternas
{
    Task<IReadOnlyList<ListaExterna>> ListarAsync(CancellationToken ct = default);
    Task<ListaExterna?> ObterPorIdAsync(int id, CancellationToken ct = default);
    void Adicionar(ListaExterna lista);

    /// <summary>
    /// Identifica o estado atual das listas (ativa, ultima atualizacao). Muda sempre que o conteudo aplicavel muda;
    /// permite ao Worker so recarregar as entradas quando necessario.
    /// </summary>
    Task<string> ObterVersaoAsync(CancellationToken ct = default);

    /// <summary>Troca todas as entradas da lista de uma vez (transacao): nunca fica meio atualizada.</summary>
    Task SubstituirEntradasAsync(int listaExternaId, IReadOnlyList<EntradaListaExterna> entradas, CancellationToken ct = default);

    Task<IReadOnlyList<EntradaListaExterna>> ListarEntradasAsync(IReadOnlyCollection<int> listasIds, CancellationToken ct = default);

    /// <summary>Listas (com a referencia na fonte) que contem o endereco informado.</summary>
    Task<IReadOnlyList<(string Lista, string? Referencia)>> ListarQueContemAsync(string chaveIp, CancellationToken ct = default);

    Task<IReadOnlyList<CoincidenciaListaExterna>> ObterCoincidenciasAsync(IReadOnlyCollection<(int ListaId, string Ip)> chaves, CancellationToken ct = default);
    Task<IReadOnlyList<CoincidenciaListaExterna>> ListarCoincidenciasRecentesAsync(int quantidade, CancellationToken ct = default);
    void AdicionarCoincidencia(CoincidenciaListaExterna coincidencia);
    Task<int> RemoverCoincidenciasAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default);
}

public interface IRepositorioStatusWorker
{
    Task<StatusWorker?> ObterAsync(CancellationToken ct = default);
    void Adicionar(StatusWorker status);
}

public interface IRepositorioRegrasFirewallDesativadas
{
    Task<IReadOnlyList<RegraFirewallDesativada>> ListarAsync(CancellationToken ct = default);
    void Adicionar(RegraFirewallDesativada regra);
    void Remover(RegraFirewallDesativada regra);
}
