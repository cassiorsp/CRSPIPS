using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRSP.IPS.Infrastructure.Persistencia;

/// <summary>
/// Web e Worker acessam o mesmo arquivo SQLite. WAL permite leituras simultaneas a uma escrita;
/// busy_timeout faz a conexao aguardar em vez de falhar quando o outro processo esta gravando.
/// </summary>
internal sealed class InterceptorSqlite : DbConnectionInterceptor
{
    private const string Pragmas =
        "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        ExecutarPragmas(connection);

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var comando = connection.CreateCommand();
        comando.CommandText = Pragmas;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ExecutarPragmas(DbConnection connection)
    {
        using var comando = connection.CreateCommand();
        comando.CommandText = Pragmas;
        comando.ExecuteNonQuery();
    }
}
