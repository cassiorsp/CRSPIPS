using CRSP.IPS.Application.Abstracoes;
using CRSP.IPS.Application.Modelos;
using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.EntityFrameworkCore;

namespace CRSP.IPS.Infrastructure.Persistencia;

internal sealed class RepositorioBloqueios(ContextoIps contexto) : IRepositorioBloqueios
{
    public Task<Bloqueio?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        contexto.Bloqueios.Include(b => b.Regra).FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Bloqueio?> ObterAtivoPorIpAsync(string ip, CancellationToken ct = default) =>
        contexto.Bloqueios.FirstOrDefaultAsync(b => b.Ip == ip && b.Status == StatusBloqueio.Ativo, ct);

    public async Task<IReadOnlyList<Bloqueio>> ListarAtivosAsync(CancellationToken ct = default) =>
        await contexto.Bloqueios.Where(b => b.Status == StatusBloqueio.Ativo).ToListAsync(ct);

    public async Task<IReadOnlyList<Bloqueio>> ListarAtivosNaFaixaAsync(string inicioChave, string fimChave, CancellationToken ct = default) =>
        await contexto.Bloqueios
            .Where(b => b.Status == StatusBloqueio.Ativo &&
                        string.Compare(b.IpChave, inicioChave) >= 0 &&
                        string.Compare(b.IpChave, fimChave) <= 0)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Bloqueio>> ListarPorIpAsync(string ip, CancellationToken ct = default) =>
        await contexto.Bloqueios.AsNoTracking()
            .Include(b => b.Regra)
            .Where(b => b.Ip == ip)
            .OrderByDescending(b => b.BloqueadoEm)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Bloqueio>> ListarDesdeAsync(DateTime desdeUtc, CancellationToken ct = default) =>
        await contexto.Bloqueios.AsNoTracking().Where(b => b.BloqueadoEm >= desdeUtc).ToListAsync(ct);

    public Task<int> ContarPorIpDesdeAsync(string ip, DateTime desdeUtc, CancellationToken ct = default) =>
        contexto.Bloqueios.CountAsync(b => b.Ip == ip && b.BloqueadoEm >= desdeUtc, ct);

    public async Task<Pagina<Bloqueio>> PesquisarAsync(FiltroBloqueios filtro, CancellationToken ct = default)
    {
        var consulta = contexto.Bloqueios.AsNoTracking().Include(b => b.Regra).AsQueryable();

        var busca = filtro.Busca?.Trim();
        if (!string.IsNullOrEmpty(busca))
        {
            if (EnderecoIp.TentarConverter(busca, out var ipExato))
            {
                var texto = ipExato.ToString();
                consulta = consulta.Where(b => b.Ip == texto);
            }
            else if (FaixaIp.TentarConverter(busca, out var faixa) && faixa is not null)
            {
                var inicio = faixa.Inicio.ObterChaveOrdenavel();
                var fim = faixa.Fim.ObterChaveOrdenavel();
                consulta = consulta.Where(b => string.Compare(b.IpChave, inicio) >= 0 && string.Compare(b.IpChave, fim) <= 0);
            }
            else
            {
                consulta = consulta.Where(b => b.Ip.Contains(busca) || (b.Organizacao != null && b.Organizacao.Contains(busca)));
            }
        }

        if (filtro.Status is { } status)
            consulta = consulta.Where(b => b.Status == status);
        if (filtro.Origem is { } origem)
            consulta = consulta.Where(b => b.Origem == origem);
        if (!string.IsNullOrWhiteSpace(filtro.PaisCodigo))
            consulta = consulta.Where(b => b.PaisCodigo == filtro.PaisCodigo.ToUpper());
        if (filtro.RegraId is { } regraId)
            consulta = consulta.Where(b => b.RegraId == regraId);
        if (filtro.Simulado is { } simulado)
            consulta = consulta.Where(b => b.Simulado == simulado);
        if (filtro.DeUtc is { } de)
            consulta = consulta.Where(b => b.BloqueadoEm >= de);
        if (filtro.AteUtc is { } ate)
            consulta = consulta.Where(b => b.BloqueadoEm < ate);

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(b => b.BloqueadoEm)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .ToListAsync(ct);

        return new Pagina<Bloqueio>(itens, total, filtro.Pagina, filtro.TamanhoPagina);
    }

    public async Task<IReadOnlyList<Bloqueio>> ListarSemLocalizacaoDesdeAsync(DateTime desdeUtc, int limite, CancellationToken ct = default) =>
        await contexto.Bloqueios
            .Where(b => b.BloqueadoEm >= desdeUtc && b.PaisCodigo == null && b.Asn == null)
            .OrderByDescending(b => b.BloqueadoEm)
            .Take(limite)
            .ToListAsync(ct);

    public Task<int> RemoverHistoricoAsync(bool manterAtivosReais, CancellationToken ct = default) =>
        contexto.Bloqueios
            .Where(b => !manterAtivosReais || b.Status != StatusBloqueio.Ativo || b.Simulado)
            .ExecuteDeleteAsync(ct);

    public void Adicionar(Bloqueio bloqueio) => contexto.Bloqueios.Add(bloqueio);
}

internal sealed class RepositorioRegras(ContextoIps contexto) : IRepositorioRegras
{
    public async Task<IReadOnlyList<RegraDeteccao>> ListarAsync(CancellationToken ct = default) =>
        await contexto.Regras.OrderBy(r => r.Fonte).ThenBy(r => r.Nome).ToListAsync(ct);

    public Task<RegraDeteccao?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        contexto.Regras.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> PossuiBloqueiosAsync(int id, CancellationToken ct = default) =>
        contexto.Bloqueios.AnyAsync(b => b.RegraId == id, ct);

    public void Adicionar(RegraDeteccao regra) => contexto.Regras.Add(regra);

    public void Remover(RegraDeteccao regra) => contexto.Regras.Remove(regra);
}

internal sealed class RepositorioListas(ContextoIps contexto) : IRepositorioListas
{
    public async Task<IReadOnlyList<EntradaLista>> ListarAsync(TipoLista? tipo = null, CancellationToken ct = default) =>
        await contexto.Listas
            .Where(e => tipo == null || e.Tipo == tipo)
            .OrderBy(e => e.Tipo).ThenBy(e => e.InicioChave)
            .ToListAsync(ct);

    public Task<EntradaLista?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        contexto.Listas.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExisteAsync(TipoLista tipo, string faixa, CancellationToken ct = default) =>
        contexto.Listas.AnyAsync(e => e.Tipo == tipo && e.Faixa == faixa, ct);

    public void Adicionar(EntradaLista entrada) => contexto.Listas.Add(entrada);

    public void Remover(EntradaLista entrada) => contexto.Listas.Remove(entrada);
}

internal sealed class RepositorioConfiguracao(ContextoIps contexto) : IRepositorioConfiguracao
{
    public async Task<Configuracao> ObterAsync(CancellationToken ct = default) =>
        await contexto.Configuracoes.FirstOrDefaultAsync(c => c.Id == Configuracao.IdUnico, ct)
        ?? throw new InvalidOperationException("Configuracao nao inicializada. Execute o InicializadorBanco.");
}

internal sealed class RepositorioConfiguracaoGeoIp(ContextoIps contexto) : IRepositorioConfiguracaoGeoIp
{
    public async Task<ConfiguracaoGeoIp> ObterAsync(CancellationToken ct = default) =>
        await contexto.ConfiguracoesGeoIp.FirstOrDefaultAsync(c => c.Id == ConfiguracaoGeoIp.IdUnico, ct)
        ?? throw new InvalidOperationException("Configuracao GeoIP nao inicializada. Execute o InicializadorBanco.");
}

internal sealed class RepositorioEventos(ContextoIps contexto) : IRepositorioEventos
{
    public void AdicionarVarios(IEnumerable<EventoSeguranca> eventos) => contexto.Eventos.AddRange(eventos);

    public async Task<IReadOnlyList<EventoSeguranca>> ListarRecentesAsync(int quantidade, TipoFonte? fonte, long? aposId, CancellationToken ct = default) =>
        await contexto.Eventos.AsNoTracking()
            .Where(e => fonte == null || e.Fonte == fonte)
            .Where(e => aposId == null || e.Id > aposId)
            .OrderByDescending(e => e.Id)
            .Take(quantidade)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EventoSeguranca>> ListarPorIpAsync(string ip, int quantidade, CancellationToken ct = default) =>
        await contexto.Eventos.AsNoTracking()
            .Where(e => e.Ip == ip)
            .OrderByDescending(e => e.Id)
            .Take(quantidade)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTime>> ListarMomentosDesdeAsync(DateTime desdeUtc, CancellationToken ct = default) =>
        await contexto.Eventos.AsNoTracking().Where(e => e.OcorridoEm >= desdeUtc).Select(e => e.OcorridoEm).ToListAsync(ct);

    public async Task<IReadOnlyList<ItemRanking>> RankingUrlsDesdeAsync(DateTime desdeUtc, int quantidade, CancellationToken ct = default)
    {
        var linhas = await contexto.Eventos.AsNoTracking()
            .Where(e => e.OcorridoEm >= desdeUtc && e.Url != null)
            .GroupBy(e => e.Url!)
            .Select(g => new { Url = g.Key, Quantidade = g.Count() })
            .OrderByDescending(g => g.Quantidade)
            .Take(quantidade)
            .ToListAsync(ct);
        return linhas.Select(l => new ItemRanking(l.Url, l.Quantidade)).ToList();
    }

    public async Task<IReadOnlyList<ItemRanking>> RankingIpsDesdeAsync(DateTime desdeUtc, int quantidade, CancellationToken ct = default)
    {
        var linhas = await contexto.Eventos.AsNoTracking()
            .Where(e => e.OcorridoEm >= desdeUtc)
            .GroupBy(e => e.Ip)
            .Select(g => new { Ip = g.Key, Quantidade = g.Count(), Pais = g.Max(e => e.PaisCodigo) })
            .OrderByDescending(g => g.Quantidade)
            .Take(quantidade)
            .ToListAsync(ct);
        return linhas.Select(l => new ItemRanking(l.Ip, l.Quantidade, l.Pais)).ToList();
    }

    public async Task<IReadOnlyList<ItemRanking>> ContarPorFonteDesdeAsync(DateTime desdeUtc, CancellationToken ct = default)
    {
        var linhas = await contexto.Eventos.AsNoTracking()
            .Where(e => e.OcorridoEm >= desdeUtc)
            .GroupBy(e => e.Fonte)
            .Select(g => new { Fonte = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);
        return linhas.Select(l => new ItemRanking(l.Fonte.ToString(), l.Quantidade)).ToList();
    }

    public Task<int> ContarPaisesDistintosDesdeAsync(DateTime desdeUtc, CancellationToken ct = default) =>
        contexto.Eventos.Where(e => e.OcorridoEm >= desdeUtc && e.PaisCodigo != null).Select(e => e.PaisCodigo).Distinct().CountAsync(ct);

    public Task<int> RemoverAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default) =>
        contexto.Eventos.Where(e => e.OcorridoEm < limiteUtc).ExecuteDeleteAsync(ct);

    public Task<int> RemoverTodosAsync(CancellationToken ct = default) => contexto.Eventos.ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<EventoSeguranca>> ListarSemPaisDesdeAsync(DateTime desdeUtc, int limite, CancellationToken ct = default) =>
        await contexto.Eventos
            .Where(e => e.OcorridoEm >= desdeUtc && e.PaisCodigo == null)
            .OrderByDescending(e => e.Id)
            .Take(limite)
            .ToListAsync(ct);
}

internal sealed class RepositorioUsuarios(ContextoIps contexto) : IRepositorioUsuarios
{
    public Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        contexto.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken ct = default) =>
        await contexto.Usuarios.AsNoTracking().OrderBy(u => u.Nome).ToListAsync(ct);

    public Task<bool> ExisteAlgumAsync(CancellationToken ct = default) => contexto.Usuarios.AnyAsync(ct);

    public async Task<IReadOnlyList<string>> ListarIpsDeAcessoDesdeAsync(DateTime desdeUtc, CancellationToken ct = default) =>
        await contexto.Usuarios.AsNoTracking()
            .Where(u => u.Ativo && u.UltimoIp != null && u.UltimoAcessoEm >= desdeUtc)
            .Select(u => u.UltimoIp!)
            .Distinct()
            .ToListAsync(ct);

    public void Adicionar(Usuario usuario) => contexto.Usuarios.Add(usuario);
}

internal sealed class RepositorioAuditoria(ContextoIps contexto) : IRepositorioAuditoria
{
    public void Adicionar(RegistroAuditoria registro) => contexto.Auditoria.Add(registro);

    public async Task<Pagina<RegistroAuditoria>> PesquisarAsync(string? texto, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        var consulta = contexto.Auditoria.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            texto = texto.Trim();
            consulta = consulta.Where(a => a.Usuario.Contains(texto) || a.Acao.Contains(texto) || a.Alvo.Contains(texto) ||
                                           (a.Detalhe != null && a.Detalhe.Contains(texto)));
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta.OrderByDescending(a => a.Id).Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToListAsync(ct);
        return new Pagina<RegistroAuditoria>(itens, total, pagina, tamanhoPagina);
    }
}

internal sealed class RepositorioPosicoesLeitura(ContextoIps contexto) : IRepositorioPosicoesLeitura
{
    public Task<PosicaoLeitura?> ObterAsync(string chave, CancellationToken ct = default) =>
        contexto.PosicoesLeitura.FirstOrDefaultAsync(p => p.Chave == chave, ct);

    public async Task<IReadOnlyList<PosicaoLeitura>> ListarAsync(CancellationToken ct = default) =>
        await contexto.PosicoesLeitura.AsNoTracking().OrderByDescending(p => p.AtualizadaEm).ToListAsync(ct);

    public void Adicionar(PosicaoLeitura posicao) => contexto.PosicoesLeitura.Add(posicao);
}

internal sealed class RepositorioStatusWorker(ContextoIps contexto) : IRepositorioStatusWorker
{
    public Task<StatusWorker?> ObterAsync(CancellationToken ct = default) =>
        contexto.StatusWorker.FirstOrDefaultAsync(s => s.Id == StatusWorker.IdUnico, ct);

    public void Adicionar(StatusWorker status) => contexto.StatusWorker.Add(status);
}

internal sealed class RepositorioListasExternas(ContextoIps contexto) : IRepositorioListasExternas
{
    public async Task<IReadOnlyList<ListaExterna>> ListarAsync(CancellationToken ct = default) =>
        await contexto.ListasExternas.OrderBy(l => l.Id).ToListAsync(ct);

    public Task<ListaExterna?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        contexto.ListasExternas.FirstOrDefaultAsync(l => l.Id == id, ct);

    public void Adicionar(ListaExterna lista) => contexto.ListasExternas.Add(lista);

    public async Task<string> ObterVersaoAsync(CancellationToken ct = default)
    {
        var estados = await contexto.ListasExternas.AsNoTracking()
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.Modo, l.UltimaAtualizacaoEm })
            .ToListAsync(ct);
        return string.Join(';', estados.Select(e => $"{e.Id}:{e.Modo}:{e.UltimaAtualizacaoEm:O}"));
    }

    public async Task SubstituirEntradasAsync(int listaExternaId, IReadOnlyList<EntradaListaExterna> entradas, CancellationToken ct = default)
    {
        await using var transacao = await contexto.Database.BeginTransactionAsync(ct);
        await contexto.EntradasListasExternas.Where(e => e.ListaExternaId == listaExternaId).ExecuteDeleteAsync(ct);
        contexto.EntradasListasExternas.AddRange(entradas);
        await contexto.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<EntradaListaExterna>> ListarEntradasAsync(IReadOnlyCollection<int> listasIds, CancellationToken ct = default) =>
        await contexto.EntradasListasExternas.AsNoTracking().Where(e => listasIds.Contains(e.ListaExternaId)).ToListAsync(ct);

    public async Task<IReadOnlyList<(string Lista, string? Referencia)>> ListarQueContemAsync(string chaveIp, CancellationToken ct = default)
    {
        var linhas = await contexto.EntradasListasExternas.AsNoTracking()
            .Where(e => string.Compare(e.InicioChave, chaveIp) <= 0 && string.Compare(e.FimChave, chaveIp) >= 0)
            .Join(contexto.ListasExternas, e => e.ListaExternaId, l => l.Id, (e, l) => new { l.Nome, e.Referencia })
            .Distinct()
            .ToListAsync(ct);
        return linhas.Select(l => (l.Nome, l.Referencia)).ToList();
    }

    public async Task<IReadOnlyList<CoincidenciaListaExterna>> ObterCoincidenciasAsync(IReadOnlyCollection<(int ListaId, string Ip)> chaves, CancellationToken ct = default)
    {
        var ips = chaves.Select(c => c.Ip).Distinct().ToList();
        var candidatas = await contexto.CoincidenciasListasExternas.Where(c => ips.Contains(c.Ip)).ToListAsync(ct);
        var desejadas = chaves.ToHashSet();
        return candidatas.Where(c => desejadas.Contains((c.ListaExternaId, c.Ip))).ToList();
    }

    public async Task<IReadOnlyList<CoincidenciaListaExterna>> ListarCoincidenciasRecentesAsync(int quantidade, CancellationToken ct = default) =>
        await contexto.CoincidenciasListasExternas.AsNoTracking()
            .OrderByDescending(c => c.UltimaEm)
            .Take(quantidade)
            .ToListAsync(ct);

    public void AdicionarCoincidencia(CoincidenciaListaExterna coincidencia) => contexto.CoincidenciasListasExternas.Add(coincidencia);

    public Task<int> RemoverCoincidenciasAnterioresAsync(DateTime limiteUtc, CancellationToken ct = default) =>
        contexto.CoincidenciasListasExternas.Where(c => c.UltimaEm < limiteUtc).ExecuteDeleteAsync(ct);
}

internal sealed class RepositorioRegrasFirewallDesativadas(ContextoIps contexto) : IRepositorioRegrasFirewallDesativadas
{
    public async Task<IReadOnlyList<RegraFirewallDesativada>> ListarAsync(CancellationToken ct = default) =>
        await contexto.RegrasFirewallDesativadas.ToListAsync(ct);

    public void Adicionar(RegraFirewallDesativada regra) => contexto.RegrasFirewallDesativadas.Add(regra);

    public void Remover(RegraFirewallDesativada regra) => contexto.RegrasFirewallDesativadas.Remove(regra);
}
