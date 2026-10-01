using ClientAutomationFramework.Core.Database;
using Dapper;

namespace ClientAutomationFramework.Tests.Database;

[TestFixture]
public sealed class RequestDataReaderTests
{
    private InMemoryDatabase database = null!;
    private RequestDataReader reader = null!;

    [SetUp]
    public async Task SetUp()
    {
        database = await InMemoryDatabase.CreateAsync();
        reader = new RequestDataReader(database, database.Settings);

        await using var connection = await database.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO XML_Requests (Scenario_id, Quote_ref, XML_Request) VALUES (@ScenarioId, @QuoteRef, @RequestBody);",
            new[]
            {
                new { ScenarioId = "SCN-001", QuoteRef = "Q-1", RequestBody = "<a/>" },
                new { ScenarioId = "SCN-002", QuoteRef = "Q-2", RequestBody = "<b/>" },
            });
    }

    [TearDown]
    public async Task TearDown() => await database.DisposeAsync();

    [Test]
    public async Task With_no_filter_returns_every_row()
    {
        var requests = await reader.GetRequestsAsync();

        Assert.That(requests.Select(r => r.ScenarioId), Is.EquivalentTo(["SCN-001", "SCN-002"]));
    }

    [Test]
    public async Task With_a_scenario_id_returns_only_that_scenarios_row()
    {
        var requests = await reader.GetRequestsAsync("SCN-002");

        Assert.That(requests, Has.Count.EqualTo(1));
        Assert.That(requests[0].QuoteRef, Is.EqualTo("Q-2"));
        Assert.That(requests[0].RequestBody, Is.EqualTo("<b/>"));
    }
}
