namespace FuzzyPricingMatcher.Tests.Database;

public sealed record SqlCommandPlan(string Sql, object Parameters);

public sealed class SqlCommandPlanFactory
{
    private readonly string requestTable;
    private readonly string responseTable;

    public SqlCommandPlanFactory(string requestTable, string responseTable)
    {
        this.requestTable = SafeSqlIdentifierValidator.Validate(requestTable);
        this.responseTable = SafeSqlIdentifierValidator.Validate(responseTable);
    }

    public IReadOnlyList<SqlCommandPlan> InsertBaseline(DatabaseInsertCommand command) => new[]
    {
        new SqlCommandPlan($"INSERT INTO {requestTable} (Scenario_id, Quote_ref, XML_request, Test_tags, Created_date) VALUES (@ScenarioId, @QuoteRef, @XmlRequest, @TestTags, SYSUTCDATETIME());", command),
        new SqlCommandPlan($"INSERT INTO {responseTable} (Scenario_id, Quote_ref, XML_response, Build_id, Created_date, Last_updated, Status) VALUES (@ScenarioId, @QuoteRef, @XmlResponse, @BuildId, SYSUTCDATETIME(), NULL, NULL);", command)
    };

    public IReadOnlyList<SqlCommandPlan> UpdateBaseline(DatabaseUpdateCommand command) => new[]
    {
        new SqlCommandPlan($"UPDATE {requestTable} SET Quote_ref = @QuoteRef, XML_request = @XmlRequest, Test_tags = @TestTags WHERE Scenario_id = @ScenarioId;", command),
        new SqlCommandPlan($"UPDATE {responseTable} SET Quote_ref = @QuoteRef, XML_response = @XmlResponse, Build_id = @BuildId, Last_updated = SYSUTCDATETIME(), Status = NULL WHERE Scenario_id = @ScenarioId;", command)
    };

    public SqlCommandPlan UpdateTags(DatabaseTagsUpdateCommand command) => new($"UPDATE {requestTable} SET Test_tags = @TestTags WHERE Scenario_id = @ScenarioId;", command);

    public IReadOnlyList<SqlCommandPlan> DeleteObsolete(string scenarioId) => new[]
    {
        new SqlCommandPlan($"DELETE FROM {responseTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId }),
        new SqlCommandPlan($"DELETE FROM {requestTable} WHERE Scenario_id = @ScenarioId;", new { ScenarioId = scenarioId })
    };

    public SqlCommandPlan UpdateComparisonPass(DatabaseComparisonPassCommand command) => new($"UPDATE {responseTable} SET XML_response = @XmlResponse, Build_id = @BuildId, Status = 'Pass', Last_updated = SYSUTCDATETIME() WHERE Scenario_id = @ScenarioId;", command);

    public SqlCommandPlan UpdateComparisonFail(DatabaseComparisonFailCommand command) => new($"UPDATE {responseTable} SET Build_id = @BuildId, Status = 'Fail', Last_updated = SYSUTCDATETIME() WHERE Scenario_id = @ScenarioId;", command);
}