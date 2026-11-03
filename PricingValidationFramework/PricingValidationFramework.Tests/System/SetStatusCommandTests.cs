using System.Text.Json;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.DataLoader;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
[NonParallelizable]
public class SetStatusCommandTests
{
    private string directory = string.Empty;
    private string settings = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"set-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        settings = Path.Combine(directory, "settings.json");
        File.WriteAllText(settings, """{ "DatabaseSettings": { "ConnectionString": "Server=test;", "AuditUser": "tester@example.com" } }""");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void Scenario_ids_should_split_on_commas_semicolons_and_lines_and_drop_blanks_and_duplicates()
    {
        var ids = SetStatusCommand.ParseScenarioIds(" S1, S2 ;s1,,\nS3\r\n  ");

        Assert.That(ids, Is.EqualTo(new[] { "S1", "S2", "S3" }));
    }

    [TestCase("")]
    [TestCase(" , ; ")]
    public void No_scenario_ids_should_be_rejected(string text)
    {
        Assert.That(() => SetStatusCommand.ParseScenarioIds(text), Throws.ArgumentException.With.Message.Contains("No scenario IDs"));
    }

    [Test]
    public void Too_many_scenario_ids_should_be_rejected()
    {
        var text = string.Join(',', Enumerable.Range(0, SetStatusCommand.MaxScenarioIds + 1).Select(index => $"S{index}"));

        Assert.That(() => SetStatusCommand.ParseScenarioIds(text), Throws.ArgumentException.With.Message.Contains("limit"));
    }

    [Test]
    public async Task Run_should_set_the_status_report_each_id_and_record_who_changed_it()
    {
        var updater = new FakeUpdater { Existing = { ["S1"] = "FAIL", ["S2"] = "PASS" } };
        var (exitCode, summary, markdown) = await RunAsync(updater, "--status", "pass", "--scenarios", "S1,S2");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(updater.Calls.Single(), Is.EqualTo(("PASS", "S1,S2")), "status is normalised to upper case");
            Assert.That(summary.GetProperty("ChangedBy").GetString(), Is.EqualTo("tester@example.com"));
            Assert.That(summary.GetProperty("Updated").GetInt32(), Is.EqualTo(1));
            Assert.That(summary.GetProperty("AlreadySet").GetInt32(), Is.EqualTo(1));
            Assert.That(summary.GetProperty("Results")[0].GetProperty("Outcome").GetString(), Is.EqualTo("Updated"));
            Assert.That(markdown, Does.Contain("| S1 | FAIL | PASS | Updated |"));
            Assert.That(markdown, Does.Contain("| S2 | PASS | PASS | Already set |"));
        });
    }

    [Test]
    public async Task Unknown_ids_should_fail_the_run_but_still_update_the_others()
    {
        var updater = new FakeUpdater { Existing = { ["S1"] = "PASS" } };
        var (exitCode, summary, markdown) = await RunAsync(updater, "--status", "FAIL", "--scenarios", "S1,MISSING");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(summary.GetProperty("Updated").GetInt32(), Is.EqualTo(1));
            Assert.That(summary.GetProperty("NotFound").GetInt32(), Is.EqualTo(1));
            Assert.That(markdown, Does.Contain("| MISSING | - | - | Not found |"));
        });
    }

    [Test]
    public async Task Ids_can_come_from_a_file_as_well()
    {
        var file = Path.Combine(directory, "ids.txt");
        File.WriteAllText(file, "S2\nS3\n");
        var updater = new FakeUpdater { Existing = { ["S1"] = "FAIL", ["S2"] = "FAIL", ["S3"] = "FAIL" } };

        await RunAsync(updater, "--status", "PASS", "--scenarios", "S1", "--scenarios-file", file);

        Assert.That(updater.Calls.Single().Ids, Is.EqualTo("S1,S2,S3"));
    }

    [TestCase("scenario_id\nS1\nS2\n", TestName = "Csv with a scenario_id header")]
    [TestCase("\uFEFFScenario_ID\r\nS1\r\nS2\r\n", TestName = "Csv saved by Excel: byte-order mark, header in any case, Windows line endings")]
    [TestCase("S1\nS2\n", TestName = "Csv without a header")]
    [TestCase("\"S1\"\n\"S2\"\n", TestName = "Csv with quoted values")]
    [TestCase("scenario_id,xml\nS1,\"<Message>a,b</Message>\"\nS2,\"<Message/>\"\n", TestName = "Csv with other columns ignored")]
    [TestCase("notes,scenario_id\nfirst,S1\nsecond,S2\n", TestName = "Csv with scenario_id in another column")]
    [TestCase("scenario_id\nS1\n\n ,\nS2\n", TestName = "Csv with blank rows skipped")]
    public async Task A_csv_file_should_supply_the_scenario_ids(string csv)
    {
        var file = Path.Combine(directory, "ids.csv");
        await File.WriteAllTextAsync(file, csv);

        Assert.That(await SetStatusCommand.ReadCsvScenarioIdsAsync(file, CancellationToken.None), Is.EqualTo(new[] { "S1", "S2" }));
    }

    [Test]
    public async Task Every_id_in_a_csv_should_get_the_one_status_from_the_run()
    {
        var file = Path.Combine(directory, "ids.csv");
        await File.WriteAllTextAsync(file, "scenario_id\nS1\nS2\nS1\n");
        var updater = new FakeUpdater { Existing = { ["S1"] = "PASS", ["S2"] = "PASS", ["S3"] = "PASS" } };

        var (exitCode, summary, _) = await RunAsync(updater, "--status", "FAIL", "--scenarios", "S3", "--scenarios-file", file);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(updater.Calls.Single(), Is.EqualTo(("FAIL", "S3,S1,S2")), "the header is skipped, duplicates removed, and the text box IDs merged");
            Assert.That(summary.GetProperty("Updated").GetInt32(), Is.EqualTo(3));
        });
    }

    [TestCase("OPEN")]
    [TestCase("")]
    public async Task Any_status_other_than_pass_or_fail_should_be_rejected_before_touching_the_database(string status)
    {
        var updater = new FakeUpdater();
        var (exitCode, summary, markdown) = await RunAsync(updater, "--status", status, "--scenarios", "S1");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(updater.Calls, Is.Empty);
            Assert.That(summary.GetProperty("Issues")[0].GetString(), Does.Contain("PASS or FAIL"));
            Assert.That(markdown, Does.Contain("**Error:**"));
        });
    }

    [Test]
    public void The_updater_should_reject_any_status_other_than_pass_or_fail()
    {
        var updater = new ScenarioStatusUpdater(new SqlConnectionFactory(new DatabaseSettings()));

        Assert.That(async () => await updater.SetStatusAsync(["S1"], "pass"), Throws.ArgumentException);
    }

    private async Task<(int ExitCode, JsonElement Summary, string Markdown)> RunAsync(FakeUpdater updater, params string[] args)
    {
        var report = Path.Combine(directory, "summary.json");
        var markdown = Path.Combine(directory, "summary.md");
        var exitCode = await SetStatusCommand.RunAsync(
            [.. args, "--settings", settings, "--report", report, "--markdown", markdown],
            (_, _) => updater,
            CancellationToken.None);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(report));
        return (exitCode, json.RootElement.Clone(), await File.ReadAllTextAsync(markdown));
    }

    private sealed class FakeUpdater : IScenarioStatusUpdater
    {
        public Dictionary<string, string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string Status, string Ids)> Calls { get; } = [];

        public Task<IReadOnlyList<ScenarioStatusChange>> SetStatusAsync(
            IReadOnlyCollection<string> scenarioIds, string status, CancellationToken cancellationToken = default)
        {
            Calls.Add((status, string.Join(',', scenarioIds)));
            return Task.FromResult<IReadOnlyList<ScenarioStatusChange>>(scenarioIds.Select(id =>
                !Existing.TryGetValue(id, out var previous) ? new ScenarioStatusChange(id, null, status, ScenarioStatusOutcome.NotFound)
                : previous == status ? new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.AlreadySet)
                : new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.Updated)).ToArray());
        }
    }
}
