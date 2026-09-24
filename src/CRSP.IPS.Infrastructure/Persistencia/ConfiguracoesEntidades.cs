using CRSP.IPS.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRSP.IPS.Infrastructure.Persistencia;

internal sealed class ConfiguracaoBloqueio : IEntityTypeConfiguration<Bloqueio>
{
    public void Configure(EntityTypeBuilder<Bloqueio> builder)
    {
        builder.ToTable("Bloqueios");
        builder.Property(b => b.Ip).HasMaxLength(45).IsRequired();
        builder.Property(b => b.IpChave).HasMaxLength(32).IsRequired();
        builder.Property(b => b.Motivo).HasMaxLength(200).IsRequired();
        builder.Property(b => b.PaisCodigo).HasMaxLength(2);
        builder.Property(b => b.PaisNome).HasMaxLength(100);
        builder.Property(b => b.Cidade).HasMaxLength(100);
        builder.Property(b => b.Organizacao).HasMaxLength(200);
        builder.Property(b => b.CriadoPor).HasMaxLength(200).IsRequired();
        builder.Property(b => b.EncerradoPor).HasMaxLength(200);
        builder.Property(b => b.Comentario).HasMaxLength(1000);
        builder.HasOne(b => b.Regra).WithMany().HasForeignKey(b => b.RegraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.Status, b.Simulado });
        builder.HasIndex(b => b.Ip);
        builder.HasIndex(b => b.IpChave);
        builder.HasIndex(b => b.BloqueadoEm);
        builder.Ignore(b => b.EhPermanente);
        builder.Ignore(b => b.EstaPendenteNoFirewall);
    }
}

internal sealed class ConfiguracaoRegra : IEntityTypeConfiguration<RegraDeteccao>
{
    public void Configure(EntityTypeBuilder<RegraDeteccao> builder)
    {
        builder.ToTable("Regras");
        builder.Property(r => r.Nome).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Descricao).HasMaxLength(500);
        builder.Property(r => r.Padrao).HasMaxLength(1000).IsRequired();
        builder.HasIndex(r => r.Nome).IsUnique();
        builder.Ignore(r => r.Janela);
    }
}

internal sealed class ConfiguracaoEvento : IEntityTypeConfiguration<EventoSeguranca>
{
    public void Configure(EntityTypeBuilder<EventoSeguranca> builder)
    {
        builder.ToTable("Eventos");
        builder.Property(e => e.Ip).HasMaxLength(45).IsRequired();
        builder.Property(e => e.Metodo).HasMaxLength(16);
        builder.Property(e => e.Url).HasMaxLength(2048);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.Detalhe).HasMaxLength(512);
        builder.Property(e => e.PaisCodigo).HasMaxLength(2);
        builder.Property(e => e.Site).HasMaxLength(200);
        builder.HasIndex(e => e.OcorridoEm);
        builder.HasIndex(e => e.Ip);
    }
}

internal sealed class ConfiguracaoEntradaLista : IEntityTypeConfiguration<EntradaLista>
{
    public void Configure(EntityTypeBuilder<EntradaLista> builder)
    {
        builder.ToTable("Listas");
        builder.Property(e => e.Faixa).HasMaxLength(100).IsRequired();
        builder.Property(e => e.InicioChave).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FimChave).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Descricao).HasMaxLength(300);
        builder.Property(e => e.CriadaPor).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => new { e.Tipo, e.Faixa }).IsUnique();
    }
}

internal sealed class ConfiguracaoConfiguracao : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("Configuracao");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TemposBloqueio).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CaminhoLogIis).HasMaxLength(500).IsRequired();
        builder.Property(c => c.CaminhoHttpErr).HasMaxLength(500).IsRequired();
        builder.Property(c => c.PaisesPolitica).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.PortasPolitica).HasMaxLength(200).IsRequired();
        builder.Property(c => c.AtualizadaPor).HasMaxLength(200);
    }
}

internal sealed class ConfiguracaoConfiguracaoGeoIp : IEntityTypeConfiguration<ConfiguracaoGeoIp>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoGeoIp> builder)
    {
        builder.ToTable("ConfiguracaoGeoIp");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.ContaId).HasMaxLength(20);
        builder.Property(c => c.ChaveProtegida).HasMaxLength(2000);
        builder.Property(c => c.FinalChave).HasMaxLength(4);
        builder.Property(c => c.UltimoResultado).HasMaxLength(500);
        builder.Ignore(c => c.PossuiCredenciais);
    }
}

internal sealed class ConfiguracaoUsuario : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.Property(u => u.Nome).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.HashSenha).HasMaxLength(500).IsRequired();
        builder.Property(u => u.UltimoIp).HasMaxLength(45);
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class ConfiguracaoAuditoria : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("Auditoria");
        builder.Property(a => a.Usuario).HasMaxLength(200).IsRequired();
        builder.Property(a => a.IpOrigem).HasMaxLength(45);
        builder.Property(a => a.Acao).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Alvo).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Detalhe).HasMaxLength(1000);
        builder.HasIndex(a => a.OcorridoEm);
    }
}

internal sealed class ConfiguracaoPosicaoLeitura : IEntityTypeConfiguration<PosicaoLeitura>
{
    public void Configure(EntityTypeBuilder<PosicaoLeitura> builder)
    {
        builder.ToTable("PosicoesLeitura");
        builder.HasKey(p => p.Chave);
        builder.Property(p => p.Chave).HasMaxLength(500);
    }
}

internal sealed class ConfiguracaoStatusWorker : IEntityTypeConfiguration<StatusWorker>
{
    public void Configure(EntityTypeBuilder<StatusWorker> builder)
    {
        builder.ToTable("StatusWorker");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Maquina).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Versao).HasMaxLength(50).IsRequired();
        builder.Property(s => s.UltimoErro).HasMaxLength(1000);
        builder.Property(s => s.ResumoPoliticaPaises).HasMaxLength(500);
    }
}

internal sealed class ConfiguracaoListaExterna : IEntityTypeConfiguration<ListaExterna>
{
    public void Configure(EntityTypeBuilder<ListaExterna> builder)
    {
        builder.ToTable("ListasExternas");
        builder.Property(l => l.Nome).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Descricao).HasMaxLength(500);
        builder.Property(l => l.Urls).HasMaxLength(2000).IsRequired();
        builder.Property(l => l.UltimoErro).HasMaxLength(500);
        builder.HasIndex(l => l.Nome).IsUnique();
        builder.Ignore(l => l.ProximaAtualizacaoEm);
    }
}

internal sealed class ConfiguracaoEntradaListaExterna : IEntityTypeConfiguration<EntradaListaExterna>
{
    public void Configure(EntityTypeBuilder<EntradaListaExterna> builder)
    {
        builder.ToTable("EntradasListasExternas");
        builder.Property(e => e.Faixa).HasMaxLength(100).IsRequired();
        builder.Property(e => e.InicioChave).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FimChave).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Referencia).HasMaxLength(50);
        builder.HasOne<ListaExterna>().WithMany().HasForeignKey(e => e.ListaExternaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ListaExternaId, e.InicioChave });
    }
}

internal sealed class ConfiguracaoCoincidenciaListaExterna : IEntityTypeConfiguration<CoincidenciaListaExterna>
{
    public void Configure(EntityTypeBuilder<CoincidenciaListaExterna> builder)
    {
        builder.ToTable("CoincidenciasListasExternas");
        builder.Property(c => c.Ip).HasMaxLength(45).IsRequired();
        builder.Property(c => c.UltimoSite).HasMaxLength(200);
        builder.Property(c => c.UltimaUrl).HasMaxLength(500);
        builder.HasOne<ListaExterna>().WithMany().HasForeignKey(c => c.ListaExternaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.ListaExternaId, c.Ip }).IsUnique();
        builder.HasIndex(c => c.UltimaEm);
    }
}

internal sealed class ConfiguracaoRegraFirewallDesativada : IEntityTypeConfiguration<RegraFirewallDesativada>
{
    public void Configure(EntityTypeBuilder<RegraFirewallDesativada> builder)
    {
        builder.ToTable("RegrasFirewallDesativadas");
        builder.Property(r => r.Nome).HasMaxLength(300).IsRequired();
        builder.HasIndex(r => r.Nome).IsUnique();
    }
}
