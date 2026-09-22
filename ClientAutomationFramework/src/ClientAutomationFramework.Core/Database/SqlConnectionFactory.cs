using ClientAutomationFramework.Core.Configuration;
using Microsoft.Data.SqlClient;

namespace ClientAutomationFramework.Core.Database;

public sealed class SqlConnectionFactory(DatabaseSettings settings)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("A SQL connection string is required.");
        var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
