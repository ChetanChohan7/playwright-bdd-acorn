using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Models;
using Dapper;

namespace ClientAutomationFramework.Core.Database;

public sealed class ResponseDataReader(IDbConnectionFactory connectionFactory, DatabaseSettings settings)
{
    private readonly string responseTable = SafeIdentifier.Validate(settings.ResponseTableName);

    /// The response table is append-only history: a re-run of the same scenario_id can carry a
    /// new quote_ref, so "the previous response" is looked up by scenario_id alone, ordered by
    /// Created_date. Returns null the first time a scenario is seen.
    ///
    /// Takes the first row in C# rather than a SQL-level TOP (1)/LIMIT 1, since those aren't
    /// portable across the SQL Server this runs against in production and the in-memory SQLite
    /// used in Database/*Tests.cs - the per-scenario row count is small enough that this costs
    /// nothing in practice.
    public async Task<ScenarioResponse?> GetLastResponseAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var sql = $"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_Response AS ResponseBody, Status, Created_date AS CreatedDate FROM {responseTable} WHERE Scenario_id = @ScenarioId ORDER BY Created_date DESC, Id DESC;";
        var rows = await connection.QueryAsync<ScenarioResponse>(new CommandDefinition(sql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken));
        return rows.FirstOrDefault();
    }
}
