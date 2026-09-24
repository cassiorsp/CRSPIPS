using Donc.IPS.Application.Abstracoes;
using Donc.IPS.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Donc.IPS.Infrastructure.Persistencia;

public sealed class ContextoIps(DbContextOptions<ContextoIps> options) : DbContext(options), IUnidadeDeTrabalho
{
    public DbSet<Bloqueio> Bloqueios => Set<Bloqueio>();
    public DbSet<RegraDeteccao> Regras => Set<RegraDeteccao>();
    public DbSet<EventoSeguranca> Eventos => Set<EventoSeguranca>();
    public DbSet<EntradaLista> Listas => Set<EntradaLista>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<ConfiguracaoGeoIp> ConfiguracoesGeoIp => Set<ConfiguracaoGeoIp>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<PosicaoLeitura> PosicoesLeitura => Set<PosicaoLeitura>();
    public DbSet<StatusWorker> StatusWorker => Set<StatusWorker>();
    public DbSet<RegraFirewallDesativada> RegrasFirewallDesativadas => Set<RegraFirewallDesativada>();
    public DbSet<ListaExterna> ListasExternas => Set<ListaExterna>();
    public DbSet<EntradaListaExterna> EntradasListasExternas => Set<EntradaListaExterna>();
    public DbSet<CoincidenciaListaExterna> CoincidenciasListasExternas => Set<CoincidenciaListaExterna>();

    public Task SalvarAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContextoIps).Assembly);

    /// <summary>SQLite nao guarda o Kind do DateTime; todas as datas do sistema sao UTC.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<ConversorDataUtc>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<ConversorDataUtc>();
    }

    private sealed class ConversorDataUtc() : ValueConverter<DateTime, DateTime>(
        valor => valor.Kind == DateTimeKind.Utc ? valor : valor.ToUniversalTime(),
        valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));
}
