using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Tests.Helpers.Setup;

namespace PricingValidationFramework.Tests.System.Ice;

[TestFixture]
public class IceDiscoveryFilterTests
{
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"ice-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [TestCase("", "")]
    [TestCase("  ", " ")]
    public async Task Ice_should_select_every_product_when_no_filter_is_set(string product, string tag)
    {
        var reader = new RecordingReader();

        await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection(product, tag, null));

        Assert.That(reader.Calls, Is.EqualTo(new[] { "all" }));
    }

    [TestCase("Carrot", "", "filtered:Carrot|")]
    [TestCase("", "smoke", "filtered:|smoke")]
    [TestCase("Carrot", "smoke", "filtered:Carrot|smoke")]
    public async Task Ice_should_use_the_same_product_and_tag_filters_as_radar(string product, string tag, string expected)
    {
        var reader = new RecordingReader();

        await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection(product, tag, null));

        Assert.That(reader.Calls, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public async Task Ice_should_select_exactly_the_scenarios_that_passed_in_the_radar_report_ignoring_filters()
    {
        var report = WriteRadarReport(
            ("S1", "PASS"), ("S1", "PASS"), ("S2", "FAIL"), ("S3", "ERROR"), ("S4", "PASS"), ("RadarDiscovery", "ERROR"));
        var reader = new RecordingReader();

        var scenarios = await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection("Carrot", "smoke", report));

        Assert.Multiple(() =>
        {
            Assert.That(reader.Calls, Is.EqualTo(new[] { "ids:S1,S4" }));
            Assert.That(scenarios.Select(scenario => scenario.ScenarioId), Is.EqualTo(new[] { "S1", "S4" }));
        });
    }

    [Test]
    public async Task Ice_should_select_nothing_when_no_scenario_passed_in_the_radar_report()
    {
        var reader = new RecordingReader();

        var scenarios = await IceTestSetup.DiscoverScenariosAsync(reader, new IceTestSetup.IceSelection("", "", WriteRadarReport(("S1", "FAIL"))));

        Assert.Multiple(() =>
        {
            Assert.That(scenarios, Is.Empty);
            Assert.That(reader.Calls, Is.Empty, "no query for an empty selection");
        });
    }

    [Test]
    public void Ice_should_fail_clearly_when_the_radar_report_is_missing()
    {
        var missing = Path.Combine(directory, "Radar_1.csv");

        Assert.That(async () => await IceTestSetup.DiscoverScenariosAsync(new RecordingReader(), new IceTestSetup.IceSelection("", "", missing)),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("Radar_1.csv"));
    }

    private string WriteRadarReport(params (string ScenarioId, string Result)[] rows)
    {
        // The real header, with multiline quoted XML in the last column as Radar writes it.
        var path = Path.Combine(directory, $"Radar_{Guid.NewGuid():N}.csv");
        var lines = new List<string> { "ScenarioId,QuoteRef,ProductCode,SchemeCode,ProfileId,FieldName,XPath,BaselineValue,ApiValue,Difference,MinDifference,MaxDifference,FieldResult,ScenarioResult,FailureStage,Error,RequestXml,BaselineXml,ApiXml" };
        lines.AddRange(rows.Select(row => $"{row.ScenarioId},Q,HOME,ABC,P1,Premium,/x,1,1,0,0,0,{row.Result},{row.Result},,,\"<a>\nPASS,x</a>\",,"));
        File.WriteAllText(path, string.Join("\n", lines));
        return path;
    }

    private sealed class RecordingReader : IBaselineDataReader
    {
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("all");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([]);
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetFilteredPassingBaselineScenariosAsync(
            string? productCode,
            string? scenarioTag,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"filtered:{productCode}|{scenarioTag}");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>([]);
        }

        public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselinesByScenarioIdsAsync(
            IReadOnlyCollection<string> scenarioIds,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"ids:{string.Join(',', scenarioIds)}");
            return Task.FromResult<IReadOnlyList<IceBaselineScenario>>(
                scenarioIds.Select(id => new IceBaselineScenario { ScenarioId = id, Status = "PASS" }).ToArray());
        }

        public Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScenarioResponse?>(null);
    }
}
