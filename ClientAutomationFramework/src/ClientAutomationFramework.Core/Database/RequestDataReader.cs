using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Models;
using Dapper;

namespace ClientAutomationFramework.Core.Database;

public sealed class RequestDataReader(SqlConnectionFactory connectionFactory, DatabaseSettings settings)
{
    private readonly string requestTable = SafeIdentifier.Validate(settings.RequestTableName);

    public async Task<IReadOnlyList<ScenarioRequest>> GetRequestsAsync(string? scenarioId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var sql = $"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_Request AS RequestBody FROM {requestTable}"
            + (scenarioId is null ? ";" : " WHERE Scenario_id = @ScenarioId;");
        var rows = await connection.QueryAsync<ScenarioRequest>(new CommandDefinition(sql, scenarioId is null ? null : new { ScenarioId = scenarioId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }
}
