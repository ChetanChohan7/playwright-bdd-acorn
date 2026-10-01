using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Models;
using Dapper;

namespace ClientAutomationFramework.Core.Database;

public sealed class ResultUpdater(IDbConnectionFactory connectionFactory, DatabaseSettings settings)
{
    private readonly string responseTable = SafeIdentifier.Validate(settings.ResponseTableName);

    /// Created_date is set here (UTC, app-side) rather than left to a SQL Server default like
    /// SYSUTCDATETIME() - that kept the insert tied to SQL Server, and relying on each row's own
    /// insert time rather than the server clock makes it something a test can assert on directly.
    public async Task InsertAsync(NewScenarioResponse response, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var sql = $"INSERT INTO {responseTable} (Scenario_id, Quote_ref, XML_Response, Status, Created_date) VALUES (@ScenarioId, @QuoteRef, @ResponseBody, @Status, @CreatedDate);";
        var parameters = new
        {
            response.ScenarioId,
            response.QuoteRef,
            response.ResponseBody,
            response.Status,
            CreatedDate = DateTime.UtcNow
        };
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}
