using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CRSP.IPS.Infrastructure.Persistencia;

/// <summary>Usada apenas pelo "dotnet ef migrations add".</summary>
internal sealed class FabricaContextoDesignTime : IDesignTimeDbContextFactory<ContextoIps>
{
    public ContextoIps CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ContextoIps>().UseSqlite("Data Source=design.db").Options);
}
