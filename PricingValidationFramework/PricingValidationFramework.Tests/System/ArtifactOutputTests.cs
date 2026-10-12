using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Tests.Integration.Ice;
using PricingValidationFramework.Tests.Integration.Radar;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
[NonParallelizable]
public class ArtifactOutputTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public async Task Setup_or_discovery_failure_should_still_write_local_report_and_log(bool ice, bool discovery)
    {
        var buildId = $"artifact-test-{Guid.NewGuid():N}";
        var originalBuild = Environment.GetEnvironmentVariable("BUILD_BUILDID");
        var originalMapping = Environment.GetEnvironmentVariable("PricingProfiles__MappingFile");
        var runner = ice ? "Ice" : "Radar";
        var output = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults");
        var reportPath = Path.Combine(output, "Reports", $"{runner}_{buildId}.csv");
        var logPath = Path.Combine(output, "Logs", $"{runner}_{buildId}.log");
        Environment.SetEnvironmentVariable("BUILD_BUILDID", buildId);
        Environment.SetEnvironmentVariable("PricingProfiles__MappingFile", "missing-test-mapping.json");
        try
        {
            if (ice)
            {
                var fixture = new IceValidationTests();
                fixture.SetUp();
                if (discovery)
                    Assert.ThrowsAsync<AssertionException>(() => fixture.Ice_scenario_should_match_baseline(null, "Discovery failed."));
                else
                    Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Ice_scenario_should_match_baseline(
                        new IceBaselineScenario { ScenarioId = "S1", QuoteRef = "Q1", ProductCode = "HOME", SchemeCode = "ABC" }, null));
                await fixture.TearDown();
            }
            else
            {
                var fixture = new RadarValidationTests();
                fixture.SetUp();
                if (discovery)
                    Assert.ThrowsAsync<AssertionException>(() => fixture.Radar_scenario_should_validate(null, "Discovery failed."));
                else
                    Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Radar_scenario_should_validate(
                        new ScenarioRequest { ScenarioId = "S1", QuoteRef = "Q1", ProductCode = "HOME", SchemeCode = "ABC" }, null));
                await fixture.TearDown();
            }
            using var logStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var logReader = new StreamReader(logStream);
            var log = await logReader.ReadToEndAsync();
            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(reportPath), Is.True, reportPath);
                Assert.That(File.Exists(logPath), Is.True, logPath);
                Assert.That(File.ReadAllText(reportPath), Does.Contain(discovery ? "Discovery" : "S1").And.Contain("ERROR"));
                Assert.That(log, Does.Contain("setup failed"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUILD_BUILDID", originalBuild);
            Environment.SetEnvironmentVariable("PricingProfiles__MappingFile", originalMapping);
            File.Delete(reportPath);
            File.Delete(logPath);
        }
    }
}
