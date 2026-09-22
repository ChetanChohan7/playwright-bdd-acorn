using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Models;
using Dapper;

namespace ClientAutomationFramework.Core.Database;

public sealed class ResponseDataReader(SqlConnectionFactory connectionFactory, DatabaseSettings settings)
{
    private readonly string responseTable = SafeIdentifier.Validate(settings.ResponseTableName);

    /// The response table is append-only history: a re-run of the same scenario_id can carry a
    /// new quote_ref, so "the previous response" is looked up by scenario_id alone, ordered by
    /// Created_date. Returns null the first time a scenario is seen.
    public async Task<ScenarioResponse?> GetLastResponseAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var sql = $"SELECT TOP (1) Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_Response AS ResponseBody, Status, Created_date AS CreatedDate FROM {responseTable} WHERE Scenario_id = @ScenarioId ORDER BY Created_date DESC, Id DESC;";
        return await connection.QuerySingleOrDefaultAsync<ScenarioResponse>(new CommandDefinition(sql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken));
    }
}
