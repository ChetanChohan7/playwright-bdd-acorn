using ClientAutomationFramework.Core.Database;
using ClientAutomationFramework.Core.Models;

namespace ClientAutomationFramework.Tests.Database;

[TestFixture]
public sealed class ResultUpdaterTests
{
    private InMemoryDatabase database = null!;
    private ResultUpdater updater = null!;
    private ResponseDataReader reader = null!;

    [SetUp]
    public async Task SetUp()
    {
        database = await InMemoryDatabase.CreateAsync();
        updater = new ResultUpdater(database, database.Settings);
        reader = new ResponseDataReader(database, database.Settings);
    }

    [TearDown]
    public async Task TearDown() => await database.DisposeAsync();

    [Test]
    public async Task Inserted_row_is_readable_back_through_ResponseDataReader()
    {
        var before = DateTime.UtcNow;

        await updater.InsertAsync(new NewScenarioResponse("SCN-001", "Q-1", "<response/>", "PASS"));

        var stored = await reader.GetLastResponseAsync("SCN-001");

        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.QuoteRef, Is.EqualTo("Q-1"));
        Assert.That(stored.ResponseBody, Is.EqualTo("<response/>"));
        Assert.That(stored.Status, Is.EqualTo("PASS"));
        Assert.That(stored.CreatedDate, Is.InRange(before, DateTime.UtcNow));
    }

    [Test]
    public async Task A_later_insert_for_the_same_scenario_becomes_the_new_last_response()
    {
        await updater.InsertAsync(new NewScenarioResponse("SCN-001", "Q-1", "<first/>", "PASS"));
        await updater.InsertAsync(new NewScenarioResponse("SCN-001", "Q-2", "<second/>", "FAIL"));

        var stored = await reader.GetLastResponseAsync("SCN-001");

        Assert.That(stored!.QuoteRef, Is.EqualTo("Q-2"));
        Assert.That(stored.Status, Is.EqualTo("FAIL"));
    }
}
