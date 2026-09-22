using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Models;
using Dapper;

namespace ClientAutomationFramework.Core.Database;

public sealed class ResultUpdater(SqlConnectionFactory connectionFactory, DatabaseSettings settings)
{
    private readonly string responseTable = SafeIdentifier.Validate(settings.ResponseTableName);

    public async Task InsertAsync(NewScenarioResponse response, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var sql = $"INSERT INTO {responseTable} (Scenario_id, Quote_ref, XML_Response, Status, Created_date) VALUES (@ScenarioId, @QuoteRef, @ResponseBody, @Status, SYSUTCDATETIME());";
        await connection.ExecuteAsync(new CommandDefinition(sql, response, cancellationToken: cancellationToken));
    }
}
