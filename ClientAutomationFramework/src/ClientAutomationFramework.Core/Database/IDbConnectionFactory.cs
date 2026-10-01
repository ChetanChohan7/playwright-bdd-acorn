using System.Data.Common;

namespace ClientAutomationFramework.Core.Database;

/// Lets RequestDataReader/ResponseDataReader/ResultUpdater run against a provider other than
/// SQL Server - a real connection factory in production, an in-memory SQLite one in fast tests.
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
}
