using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Evidence;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Infrastructure;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class EvidenceAndLoggingTests
{
    [Test]
    [Category("Unit")]
    public void Safe_filename_conversion_removes_traversal_and_unsafe_characters()
    {
        var safe = SafeFileName.Convert("../SCN/001:quote");
        Assert.Multiple(() =>
        {
            Assert.That(safe, Does.Not.Contain("/"));
            Assert.That(safe, Does.Not.Contain(".."));
            Assert.That(safe, Is.EqualTo("__SCN_001_quote"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Evidence_paths_are_absolute_and_separate_scenarios_by_quote()
    {
        var root = Path.Combine(Path.GetTempPath(), $"evidence-{Guid.NewGuid():N}");
        var paths = new EvidencePathBuilder(root);
        var first = paths.ScenarioDirectory("BUILD/1", "SCN/1", "Q:one");
        var second = paths.ScenarioDirectory("BUILD/1", "SCN/1", "Q:two");
        Assert.Multiple(() =>
        {
            Assert.That(Path.IsPathFullyQualified(first), Is.True);
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(paths.BuildRoot("BUILD/1"), Does.Contain("Build-BUILD_1"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Scenario_evidence_preserves_raw_request_baseline_and_optional_api_content()
    {
        var root = Path.Combine(Path.GetTempPath(), $"evidence-{Guid.NewGuid():N}");
        try
        {
            var paths = new EvidencePathBuilder(root);
            var writer = new ScenarioEvidenceWriter(paths, "BUILD-1");
            var request = "<Request>  raw request\n</Request>";
            var baseline = "<Response> raw baseline </Response>";
            writer.Save(new ScenarioEvidence("SCN/1", "Q:1", request, baseline, null, "Failed", "API failed"));
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllText(paths.RequestXml("BUILD-1", "SCN/1", "Q:1")), Is.EqualTo(request));
                Assert.That(File.ReadAllText(paths.BaselineXml("BUILD-1", "SCN/1", "Q:1")), Is.EqualTo(baseline));
                Assert.That(File.Exists(paths.ApiXml("BUILD-1", "SCN/1", "Q:1")), Is.False);
                Assert.That(File.Exists(paths.ScenarioLog("BUILD-1", "SCN/1", "Q:1")), Is.True);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    [Category("Unit")]
    public void Loader_summary_contains_metadata_counts_and_result_lines()
    {
        var path = Path.Combine(Path.GetTempPath(), $"loader-summary-{Guid.NewGuid():N}.txt");
        try
        {
            var result = new LoaderSynchronizationResult(new LoaderScenarioResult[]
            {
                new() { ScenarioId = "SCN1", QuoteRef = "Q1", Successful = true, Outcome = LoaderScenarioOutcome.Inserted, ApiCalled = true, RequestTableAction = "Insert", ResponseTableAction = "Insert" },
                new() { ScenarioId = "SCN2", QuoteRef = "Q2", Successful = false, Outcome = LoaderScenarioOutcome.ApiFailed, Error = "failed" }
            }, "failure skipped deletion");
            new LoaderSummaryWriter().Write(path, result, new LoaderSummaryContext("BUILD-1", "TestAsset/baseline-scenarios.csv", DateTimeOffset.UtcNow));
            var text = File.ReadAllText(path);
            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("Build ID: BUILD-1"));
                Assert.That(text, Does.Contain("Total scenarios: 2"));
                Assert.That(text, Does.Contain("Inserted: 1"));
                Assert.That(text, Does.Contain("Deletion skipped: True"));
                Assert.That(text, Does.Contain("SCN1 | Q1 | True | Inserted | True | Insert | Insert"));
            });
        }
        finally { File.Delete(path); }
    }

    [Test]
    [Category("Unit")]
    public void Nlog_config_is_copied_to_test_output()
    {
        Assert.That(File.Exists(Path.Combine(TestContext.CurrentContext.TestDirectory, "nlog.config")), Is.True);
    }

    [Test]
    [Category("Unit")]
    public void NUnit_output_writer_contains_fields_but_not_xml_bodies()
    {
        var writer = new NUnitScenarioOutputWriter();
        var result = new ComparisonResult("SCN", "Q", ComparisonOutcome.Passed, true, 1m, 1m, 0m, string.Empty);
        Assert.DoesNotThrow(() => writer.Write(result, "BUILD", "S-1", "EndpointA", -0.05m, 0.05m, "C:\\evidence\\scenario.log"));
    }

    [Test]
    [Category("Unit")]
    public void Safe_log_values_redact_credentials_authorization_connection_strings_and_xml()
    {
        var value = "password=__SECRET_API_PASSWORD_SENTINEL__; Authorization: __AUTHORIZATION_SENTINEL__; Server=sql;Password=__SECRET_SQL_PASSWORD_SENTINEL__; <Request>__RAW_REQUEST_XML_SENTINEL__</Request>";
        var safe = SafeLogValue.Redact(value);
        Assert.Multiple(() =>
        {
            Assert.That(safe, Does.Not.Contain("__SECRET_API_PASSWORD_SENTINEL__"));
            Assert.That(safe, Does.Not.Contain("__AUTHORIZATION_SENTINEL__"));
            Assert.That(safe, Does.Not.Contain("__SECRET_SQL_PASSWORD_SENTINEL__"));
            Assert.That(safe, Does.Not.Contain("__RAW_REQUEST_XML_SENTINEL__"));
            Assert.That(safe, Is.EqualTo("[REDACTED_XML]"));
        });
    }

    [Test]
    [Category("Unit")]
    public void Nlog_configuration_records_exception_type_without_exception_text()
    {
        var configuration = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "nlog.config"));
        Assert.Multiple(() =>
        {
            Assert.That(configuration, Does.Contain("ExceptionType=${exception:format=type}"));
            Assert.That(configuration, Does.Not.Contain("exception:format=tostring"));
        });
    }
}
