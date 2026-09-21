using Dapper;
using FuzzyPricingMatcher.Tests.Configuration;
using Microsoft.Data.SqlClient;
using NLog;

namespace FuzzyPricingMatcher.Tests.Database;

public sealed class FuzzyMatcherRepository : IFuzzyMatcherRepository
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ISqlConnectionFactory connectionFactory;
    private readonly IDatabaseRetryExecutor retryExecutor;
    private readonly ISqlMutationExecutor mutationExecutor;
    private readonly string requestTable;
    private readonly string responseTable;
    private readonly SqlCommandPlanFactory commandFactory;

    public FuzzyMatcherRepository(DatabaseSettings settings, ISqlConnectionFactory connectionFactory, IDatabaseRetryExecutor retryExecutor, ISqlMutationExecutor mutationExecutor)
    {
        requestTable = SafeSqlIdentifierValidator.Validate(settings.RequestTableName);
        responseTable = SafeSqlIdentifierValidator.Validate(settings.ResponseTableName);
        commandFactory = new SqlCommandPlanFactory(requestTable, responseTable);
        this.connectionFactory = connectionFactory;
        this.retryExecutor = retryExecutor;
        this.mutationExecutor = mutationExecutor;
    }

    public Task<IReadOnlyList<string>> GetAllRequestScenarioIdsAsync(CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("GetAllRequestScenarioIds", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        var rows = await connection.QueryAsync<string>(Command($"SELECT Scenario_id FROM {requestTable};", cancellationToken: token));
        return (IReadOnlyList<string>)rows.ToArray();
    }, cancellationToken);

    public Task<DatabaseRequestRecord?> GetRequestAsync(string scenarioId, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("GetRequest", token => QuerySingleRequestAsync(scenarioId, token), cancellationToken);

    public Task<DatabaseResponseRecord?> GetResponseAsync(string scenarioId, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("GetResponse", token => QuerySingleResponseAsync(scenarioId, token), cancellationToken);

    public Task<DatabaseScenarioSnapshot> GetScenarioForComparisonAsync(string scenarioId, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("GetScenarioForComparison", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        var requestRows = (await connection.QueryAsync<DatabaseRequestRecord>(Command($"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_request AS XmlRequest, Test_tags AS TestTags, Created_date AS CreatedDate FROM {requestTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId }, cancellationToken: token))).ToArray();
        var responseRows = (await connection.QueryAsync<DatabaseResponseRecord>(Command($"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_response AS XmlResponse, Build_id AS BuildId, Created_date AS CreatedDate, Last_updated AS LastUpdated, Status FROM {responseTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId }, cancellationToken: token))).ToArray();
        return new DatabaseScenarioSnapshot(RequireAtMostOne(requestRows, requestTable, scenarioId), RequireAtMostOne(responseRows, responseTable, scenarioId));
    }, cancellationToken);

    public Task<IReadOnlyList<DatabaseDuplicateResult>> FindDuplicatesAsync(CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("FindDuplicates", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        var requestDuplicates = await connection.QueryAsync<DatabaseDuplicateResult>(Command($"SELECT '{requestTable}' AS TableName, Scenario_id AS ScenarioId, COUNT(*) AS RowCount FROM {requestTable} GROUP BY Scenario_id HAVING COUNT(*) > 1;", cancellationToken: token));
        var responseDuplicates = await connection.QueryAsync<DatabaseDuplicateResult>(Command($"SELECT '{responseTable}' AS TableName, Scenario_id AS ScenarioId, COUNT(*) AS RowCount FROM {responseTable} GROUP BY Scenario_id HAVING COUNT(*) > 1;", cancellationToken: token));
        return (IReadOnlyList<DatabaseDuplicateResult>)requestDuplicates.Concat(responseDuplicates).ToArray();
    }, cancellationToken);

    public Task InsertBaselineAsync(DatabaseInsertCommand command, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("InsertBaseline", token => mutationExecutor.ExecuteTransactionAsync(commandFactory.InsertBaseline(command), 1, token), cancellationToken);

    public Task UpdateBaselineAsync(DatabaseUpdateCommand command, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("UpdateBaseline", token => mutationExecutor.ExecuteTransactionAsync(commandFactory.UpdateBaseline(command), 1, token), cancellationToken);

    public Task UpdateTagsAsync(DatabaseTagsUpdateCommand command, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("UpdateTags", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        await mutationExecutor.ExecuteAsync(commandFactory.UpdateTags(command), 1, token);
    }, cancellationToken);

    public Task DeleteObsoleteAsync(IReadOnlyList<string> scenarioIds, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("DeleteObsolete", token => mutationExecutor.ExecuteTransactionAsync(scenarioIds.SelectMany(commandFactory.DeleteObsolete).ToArray(), 1, token), cancellationToken);

    public Task UpdateComparisonPassAsync(DatabaseComparisonPassCommand command, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("UpdateComparisonPass", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        await mutationExecutor.ExecuteAsync(commandFactory.UpdateComparisonPass(command), 1, token);
    }, cancellationToken);

    public Task UpdateComparisonFailAsync(DatabaseComparisonFailCommand command, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("UpdateComparisonFail", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        await mutationExecutor.ExecuteAsync(commandFactory.UpdateComparisonFail(command), 1, token);
    }, cancellationToken);

    public Task<IReadOnlyList<DatabaseScenarioSelection>> SelectScenariosAsync(string? requestedTags, TagMatchMode matchMode, CancellationToken cancellationToken = default) => retryExecutor.ExecuteAsync("SelectScenarios", async token =>
    {
        await using var connection = await OpenConnectionAsync(token);
        var tags = (requestedTags ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (matchMode is not TagMatchMode.Any and not TagMatchMode.All)
            throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, "Unsupported tag match mode.");
        var sql = tags.Length == 0
            ? $"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef FROM {requestTable};"
            : matchMode == TagMatchMode.Any
                ? $"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef FROM {requestTable} AS r WHERE EXISTS (SELECT 1 FROM STRING_SPLIT(r.Test_tags, ',') AS tag WHERE LTRIM(RTRIM(tag.value)) IN @Tags);"
                : $"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef FROM {requestTable} AS r WHERE (SELECT COUNT(DISTINCT LTRIM(RTRIM(tag.value))) FROM STRING_SPLIT(r.Test_tags, ',') AS tag WHERE LTRIM(RTRIM(tag.value)) IN @Tags) = @TagCount;";
        var rows = await connection.QueryAsync<DatabaseScenarioSelection>(Command(sql, new { Tags = tags, TagCount = tags.Length }, cancellationToken: token));
        return (IReadOnlyList<DatabaseScenarioSelection>)rows.ToArray();
    }, cancellationToken);

    private async Task<DatabaseRequestRecord?> QuerySingleRequestAsync(string scenarioId, CancellationToken token)
    {
        await using var connection = await OpenConnectionAsync(token);
        var rows = (await connection.QueryAsync<DatabaseRequestRecord>(Command($"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_request AS XmlRequest, Test_tags AS TestTags, Created_date AS CreatedDate FROM {requestTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId }, cancellationToken: token))).ToArray();
        return RequireAtMostOne(rows, requestTable, scenarioId);
    }

    private async Task<DatabaseResponseRecord?> QuerySingleResponseAsync(string scenarioId, CancellationToken token)
    {
        await using var connection = await OpenConnectionAsync(token);
        var rows = (await connection.QueryAsync<DatabaseResponseRecord>(Command($"SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef, XML_response AS XmlResponse, Build_id AS BuildId, Created_date AS CreatedDate, Last_updated AS LastUpdated, Status FROM {responseTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId }, cancellationToken: token))).ToArray();
        return RequireAtMostOne(rows, responseTable, scenarioId);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken token)
    {
        var connection = connectionFactory.Create();
        try { await connection.OpenAsync(token); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static T? RequireAtMostOne<T>(IReadOnlyList<T> rows, string table, string scenarioId) where T : class
    {
        if (rows.Count > 1)
            throw new DatabaseConsistencyException($"Table {table} has {rows.Count} rows for Scenario_id '{scenarioId}'.");
        return rows.SingleOrDefault();
    }

    private static CommandDefinition Command(string sql, object? parameters = null, CancellationToken cancellationToken = default) => new(sql, parameters, cancellationToken: cancellationToken);
}