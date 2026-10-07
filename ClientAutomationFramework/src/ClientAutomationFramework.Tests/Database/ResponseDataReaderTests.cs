using ClientAutomationFramework.Core.Database;
using Dapper;

namespace ClientAutomationFramework.Tests.Database;

[TestFixture]
public sealed class ResponseDataReaderTests
{
    private InMemoryDatabase database = null!;
    private ResponseDataReader reader = null!;

    [SetUp]
    public async Task SetUp()
    {
        database = await InMemoryDatabase.CreateAsync();
        reader = new ResponseDataReader(database, database.Settings);
    }

    [TearDown]
    public async Task TearDown() => await database.DisposeAsync();

    private async Task SeedAsync(string scenarioId, string quoteRef, string responseBody, string status, DateTime createdDate)
    {
        await using var connection = await database.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO XML_Response (Scenario_id, Quote_ref, XML_Response, Status, Created_date) VALUES (@ScenarioId, @QuoteRef, @ResponseBody, @Status, @CreatedDate);",
            new { ScenarioId = scenarioId, QuoteRef = quoteRef, ResponseBody = responseBody, Status = status, CreatedDate = createdDate });
    }

    [Test]
    public async Task Returns_null_when_the_scenario_has_no_stored_response()
    {
        var result = await reader.GetLastResponseAsync("SCN-404");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Returns_the_most_recently_created_row_for_the_scenario()
    {
        var now = DateTime.UtcNow;
        await SeedAsync("SCN-001", "Q-OLD", "<old/>", "PASS", now.AddMinutes(-10));
        await SeedAsync("SCN-001", "Q-NEW", "<new/>", "PASS", now);

        var result = await reader.GetLastResponseAsync("SCN-001");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.QuoteRef, Is.EqualTo("Q-NEW"));
        Assert.That(result.ResponseBody, Is.EqualTo("<new/>"));
    }

    [Test]
    public async Task Ignores_rows_belonging_to_other_scenarios()
    {
        await SeedAsync("SCN-OTHER", "Q-1", "<other/>", "PASS", DateTime.UtcNow);

        var result = await reader.GetLastResponseAsync("SCN-001");

        Assert.That(result, Is.Null);
    }
}
