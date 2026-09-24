using CRSP.IPS.Domain.Entidades;
using CRSP.IPS.Domain.Enums;
using CRSP.IPS.Domain.ObjetosValor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRSP.IPS.Infrastructure.Persistencia;

/// <summary>Aplica migrations e cria os dados iniciais (configuracao, regras padrao e lista branca de redes locais).</summary>
public static class InicializadorBanco
{
    public static async Task InicializarAsync(IServiceProvider servicos, CancellationToken ct = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ContextoIps>();
        var relogio = escopo.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(InicializadorBanco));

        await contexto.Database.MigrateAsync(ct);
        var agora = relogio.GetUtcNow().UtcDateTime;

        var configuracao = await contexto.Configuracoes.FirstOrDefaultAsync(ct);
        if (configuracao is null)
        {
            configuracao = new Configuracao();
            contexto.Configuracoes.Add(configuracao);
            logger.LogInformation("Configuracao padrao criada (modo simulacao ligado)");
        }

        if (!await contexto.ConfiguracoesGeoIp.AnyAsync(ct))
            contexto.ConfiguracoesGeoIp.Add(new ConfiguracaoGeoIp());

        await AplicarCatalogoRegrasAsync(contexto, configuracao, agora, logger, ct);

        var listasExistentes = (await contexto.ListasExternas.Select(l => l.Nome).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var listasNovas = CatalogoListasExternas.Listar().Where(l => !listasExistentes.Contains(l.Nome)).ToList();
        if (listasNovas.Count > 0)
        {
            contexto.ListasExternas.AddRange(listasNovas);
            logger.LogInformation("Listas externas adicionadas: {Nomes}", string.Join(", ", listasNovas.Select(l => l.Nome)));
        }

        if (!await contexto.Listas.AnyAsync(ct))
        {
            contexto.Listas.AddRange(ListaBrancaPadrao(agora));
            logger.LogInformation("Lista branca padrao criada");
        }

        await contexto.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Insere as regras das versoes do catalogo ainda nao aplicadas a este banco. Uma regra so e inserida
    /// se nao existir outra com o mesmo nome; regras excluidas pelo administrador nao voltam.
    /// </summary>
    private static async Task AplicarCatalogoRegrasAsync(ContextoIps contexto, Configuracao configuracao, DateTime agora, ILogger logger, CancellationToken ct)
    {
        if (configuracao.VersaoRegrasPadrao >= CatalogoRegrasPadrao.VersaoAtual)
            return;

        var existentes = (await contexto.Regras.Select(r => r.Nome).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var novas = CatalogoRegrasPadrao.Listar(agora)
            .Where(item => item.Versao > configuracao.VersaoRegrasPadrao && !existentes.Contains(item.Regra.Nome))
            .Select(item => item.Regra)
            .ToList();

        contexto.Regras.AddRange(novas);
        configuracao.MarcarRegrasPadraoAplicadas(CatalogoRegrasPadrao.VersaoAtual);

        if (novas.Count > 0)
            logger.LogInformation(
                "Catalogo de regras v{Versao}: {Quantidade} regras adicionadas ({Nomes})",
                CatalogoRegrasPadrao.VersaoAtual, novas.Count, string.Join(", ", novas.Select(r => r.Nome)));
    }

    private static IEnumerable<EntradaLista> ListaBrancaPadrao(DateTime agora)
    {
        var entradas = new (string Faixa, string Descricao, bool Sistema)[]
        {
            ("127.0.0.0/8", "Loopback", true),
            ("::1", "Loopback IPv6", true),
            ("169.254.0.0/16", "Link-local", true),
            ("fe80::/10", "Link-local IPv6", true),
            ("10.0.0.0/8", "Rede privada (RFC 1918)", false),
            ("172.16.0.0/12", "Rede privada (RFC 1918)", false),
            ("192.168.0.0/16", "Rede privada (RFC 1918)", false),
            ("fc00::/7", "Rede privada IPv6", false)
        };

        return entradas.Select(e => EntradaLista.Criar(TipoLista.Branca, FaixaIp.Converter(e.Faixa), e.Faixa, e.Descricao, e.Sistema, agora, "CRSPIPS"));
    }
}
