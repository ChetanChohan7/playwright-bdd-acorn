using PricingValidationFramework.ControlPanel;
using PricingValidationFramework.ControlPanel.Steps;
using PricingValidationFramework.Core.Models.Enums;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class ControlPanelTests
{
    private static readonly ControlPanelPaths Paths = new("/repo/PVF", "/repo/PVF/data/import/local-settings.json", "Debug");
    private string directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), $"control-panel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void Check_csv_should_run_dataloader_validate_without_database_settings()
    {
        var spec = LocalCommandBuilder.Build(new ValidateCsvRequest("/repo/PVF/data/import/scenarios-1.csv"), Paths, "local-1");

        Assert.Multiple(() =>
        {
            Assert.That(spec.FileName, Is.EqualTo("dotnet"));
            Assert.That(spec.Arguments, Is.EqualTo(new[]
            {
                "run", "--project", Paths.DataLoaderProject, "--configuration", "Debug", "--no-launch-profile", "--",
                "validate", "--requests", "/repo/PVF/data/import/scenarios-1.csv",
                "--report", Path.Combine(Paths.WorkFolder, "local-1-validate-summary.json")
            }));
            Assert.That(spec.Arguments, Does.Not.Contain("--settings"));
            Assert.That(spec.WorkingFolder, Is.EqualTo("/repo/PVF"));
        });
    }

    [Test]
    public void Import_and_seed_should_pass_the_settings_file_and_only_the_options_given()
    {
        var import = LocalCommandBuilder.Build(new ImportRequest("/x.csv", null, 250), Paths, "local-2");
        var seed = LocalCommandBuilder.Build(new SeedRequest("2026-10-21Z09:00:00", null), Paths, "local-3");

        Assert.Multiple(() =>
        {
            Assert.That(Options(import), Is.EqualTo(new[] { "import", "--requests", "/x.csv", "--settings", Paths.LocalSettings, "--batch-size", "250" }));
            Assert.That(Options(seed), Is.EqualTo(new[] { "seed", "--settings", Paths.LocalSettings, "--request-time", "2026-10-21Z09:00:00" }));
        });
    }

    [Test]
    public void Set_status_should_pass_status_ids_and_file_and_produce_the_markdown_table()
    {
        var spec = LocalCommandBuilder.Build(new SetStatusRequest("FAIL", "S1,S2", "/ids.csv"), Paths, "local-4");

        Assert.Multiple(() =>
        {
            Assert.That(Options(spec), Is.EqualTo(new[]
            {
                "set-status", "--status", "FAIL", "--scenarios", "S1,S2", "--scenarios-file", "/ids.csv",
                "--settings", Paths.LocalSettings, "--markdown", Path.Combine(Paths.WorkFolder, "local-4-set-status-summary.md")
            }));
            Assert.That(spec.Outputs.Select(output => output.Label), Is.EqualTo(new[] { "Summary", "Before/after table" }));
        });
    }


    [Test]
    public void Radar_should_run_the_radar_tests_with_the_given_inputs_and_clear_the_rest()
    {
        var spec = LocalCommandBuilder.Build(
            new RadarRequest(ThresholdType.Percentage, -2m, 2m, "Carrot", null, null), Paths, "local-20261021-090000-000");

        Assert.Multiple(() =>
        {
            Assert.That(spec.Arguments, Does.Contain("FullyQualifiedName~PricingValidationFramework.Tests.Integration.Radar.RadarValidationTests"));
            Assert.That(spec.Arguments[^1], Is.EqualTo("NUnit.ExplicitMode=Relaxed"));
            Assert.That(spec.Environment["RADAR_THRESHOLD_TYPE"], Is.EqualTo("Percentage"));
            Assert.That(spec.Environment["RADAR_MIN_THRESHOLD"], Is.EqualTo("-2"));
            Assert.That(spec.Environment["RADAR_MAX_THRESHOLD"], Is.EqualTo("2"));
            Assert.That(spec.Environment["TEST_TAG"], Is.EqualTo("Carrot"));
            Assert.That(spec.Environment["SCENARIO_TAG"], Is.Null, "not given, so removed and the settings file applies");
            Assert.That(spec.Environment["RADAR_REQUEST_DATETIME"], Is.Null);
            Assert.That(spec.Environment["ICE_RADAR_REPORT"], Is.Null);
            Assert.That(spec.Environment["BUILD_BUILDID"], Is.EqualTo("local-20261021-090000-000"));
            Assert.That(spec.Environment["PVF_LOCAL_SETTINGS"], Is.EqualTo(Paths.LocalSettings));
            Assert.That(spec.Environment["DOTNET_ENVIRONMENT"], Is.EqualTo("Development"));
            Assert.That(spec.Outputs.Single().Path, Is.EqualTo(Path.Combine(Paths.ReportsFolder, "Radar_local-20261021-090000-000.csv")));
        });
    }

    [Test]
    public void Ice_should_run_the_ice_tests_and_pass_the_latest_radar_report_when_asked()
    {
        var spec = LocalCommandBuilder.Build(new IceRequest(null, "smoke", "/reports/Radar_local-1.csv"), Paths, "local-5");

        Assert.Multiple(() =>
        {
            Assert.That(spec.Arguments, Does.Contain("FullyQualifiedName~PricingValidationFramework.Tests.Integration.Ice.IceValidationTests"));
            Assert.That(spec.Environment["ICE_RADAR_REPORT"], Is.EqualTo("/reports/Radar_local-1.csv"));
            Assert.That(spec.Environment["SCENARIO_TAG"], Is.EqualTo("smoke"));
            Assert.That(spec.Environment["TEST_TAG"], Is.Null);
            Assert.That(spec.Environment["RADAR_THRESHOLD_TYPE"], Is.Null);
            Assert.That(spec.Outputs.Single().Path, Does.EndWith("Ice_local-5.csv"));
        });
    }

    [TestCase(null, null)]
    [TestCase(" now ", "now")]
    [TestCase("2026-10-21Z09:30:00", "2026-10-21Z09:30:00")]
    public void Request_time_should_accept_blank_now_or_the_radar_format(string? value, string? expected)
    {
        Assert.That(StepInputs.RequestTime(value), Is.EqualTo(expected));
    }

    [TestCase("21/10/2026 09:00")]
    [TestCase("tomorrow")]
    public void Request_time_should_reject_other_formats(string value)
    {
        Assert.That(() => StepInputs.RequestTime(value), Throws.InstanceOf<StepInputException>().With.Message.Contains("yyyy-MM-ddZHH:mm:ss"));
    }

    [TestCase("-0.5", -0.5)]
    [TestCase("2", 2)]
    [TestCase(" 1.25 ", 1.25)]
    public void Thresholds_should_accept_decimals_with_a_point(string value, decimal expected)
    {
        Assert.That(StepInputs.Threshold(value, "Minimum threshold"), Is.EqualTo(expected));
    }

    [TestCase("0,5")]
    [TestCase("1,000")]
    [TestCase("£1")]
    [TestCase("2%")]
    public void Thresholds_should_reject_commas_and_symbols_rather_than_misread_them(string value)
    {
        Assert.That(() => StepInputs.Threshold(value, "Minimum threshold"), Throws.InstanceOf<StepInputException>());
    }

    [Test]
    public void Threshold_range_should_reject_min_above_max_and_allow_either_missing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => StepInputs.ThresholdRange(2m, 1m), Throws.InstanceOf<StepInputException>());
            Assert.That(() => StepInputs.ThresholdRange(null, 1m), Throws.Nothing);
            Assert.That(() => StepInputs.ThresholdRange(-1m, -1m), Throws.Nothing);
        });
    }

    [TestCase("percentage", ThresholdType.Percentage)]
    [TestCase(" Pound ", ThresholdType.Pound)]
    public void Threshold_type_should_accept_the_names_in_any_case(string value, ThresholdType expected)
    {
        Assert.That(StepInputs.ThresholdTypeName(value), Is.EqualTo(expected));
    }

    [TestCase("1")]
    [TestCase("Percent")]
    public void Threshold_type_should_reject_anything_else(string value)
    {
        Assert.That(() => StepInputs.ThresholdTypeName(value), Throws.InstanceOf<StepInputException>());
    }

    [TestCase("pass", "PASS")]
    [TestCase(" FAIL ", "FAIL")]
    public void Status_should_be_pass_or_fail(string value, string expected)
    {
        Assert.That(StepInputs.Status(value), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("OPEN")]
    public void Any_other_status_should_be_rejected(string? value)
    {
        Assert.That(() => StepInputs.Status(value), Throws.InstanceOf<StepInputException>());
    }

    [Test]
    public void Scenario_tag_and_whole_numbers_should_use_the_same_rules_as_the_commands()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StepInputs.ScenarioTag(" smoke "), Is.EqualTo("smoke"));
            Assert.That(StepInputs.ScenarioTag(""), Is.Null);
            Assert.That(() => StepInputs.ScenarioTag("smoke,regression"), Throws.InstanceOf<StepInputException>());
            Assert.That(StepInputs.WholeNumber("16", "Scenarios at once", 1, 16), Is.EqualTo(16));
            Assert.That(() => StepInputs.WholeNumber("17", "Scenarios at once", 1, 16), Throws.InstanceOf<StepInputException>());
        });
    }

    [Test]
    public void Test_results_should_be_summarised_from_the_dotnet_test_output()
    {
        var spec = LocalCommandBuilder.Build(new RadarRequest(null, null, null, null, null, null), Paths, "local-6");

        var summary = StepResultReader.Summarize(StepKind.Radar, spec,
            ["building...", "Failed!  - Failed:     2, Passed:     8, Skipped:     0, Total:    10, Duration: 3 s - PricingValidationFramework.Tests.dll (net10.0)"]);

        Assert.That(summary.Single(), Is.EqualTo("Scenarios: 10. Passed 8, failed or errored 2."));
    }

    [Test]
    public void Test_results_should_also_be_read_from_the_detailed_per_line_totals()
    {
        var spec = LocalCommandBuilder.Build(new IceRequest(null, null, null), Paths, "local-9");

        var summary = StepResultReader.Summarize(StepKind.Ice, spec,
            ["Test Run Failed.", "Total tests: 4", "     Passed: 3", "     Failed: 1", " Total time: 1.9 Seconds"]);

        Assert.That(summary.Single(), Is.EqualTo("Scenarios: 4. Passed 3, failed or errored 1."));
    }

    [Test]
    public void Import_summary_should_show_counts_and_problems_with_their_rows()
    {
        var spec = WithSummary(StepKind.Import, """
            { "RequestFile": "scenarios-1.csv", "InputRequests": 3, "InsertedRequests": 1, "UpdatedRequests": 1, "SkippedRequests": 0,
              "Issues": [ { "Source": "requests", "RecordNumber": 3, "ScenarioId": "S3", "Reason": "Request XML is malformed." } ] }
            """);

        Assert.That(StepResultReader.Summarize(StepKind.Import, spec, []), Is.EqualTo(new[]
        {
            "File: scenarios-1.csv",
            "Requests read 3: inserted 1, updated 1, unchanged 0.",
            "Problem in row 3 (S3): Request XML is malformed."
        }));
    }

    [Test]
    public void Seed_and_set_status_summaries_should_list_what_failed_or_was_not_found()
    {
        var seed = WithSummary(StepKind.Seed, """
            { "RequestTime": "2026-10-21Z09:00:00", "ScenariosWithoutBaseline": 2, "Seeded": 1, "AlreadyExisted": 0, "Failed": 1, "NotRun": 0, "Issues": [],
              "Results": [ { "ScenarioId": "S2", "Outcome": "Failed", "Stage": "RouteResolution", "Error": "No route." } ] }
            """);
        var status = WithSummary(StepKind.SetStatus, """
            { "Status": "FAIL", "ChangedBy": "me@example.com", "Updated": 1, "AlreadySet": 0, "NotFound": 1, "Issues": [],
              "Results": [ { "ScenarioId": "MISSING", "Outcome": "NotFound" } ] }
            """);

        Assert.Multiple(() =>
        {
            Assert.That(StepResultReader.Summarize(StepKind.Seed, seed, []), Does.Contain("Not seeded: S2 - RouteResolution: No route."));
            Assert.That(StepResultReader.Summarize(StepKind.SetStatus, status, []), Is.EqualTo(new[]
            {
                "Set to FAIL, recorded as me@example.com.",
                "Updated 1, already FAIL 0, not found 1.",
                "Not found: MISSING"
            }));
        });
    }

    [Test]
    public void A_missing_summary_file_should_say_so()
    {
        var spec = LocalCommandBuilder.Build(new SeedRequest(null, null), Paths with { SolutionFolder = directory }, "local-7");

        Assert.That(StepResultReader.Summarize(StepKind.Seed, spec, []).Single(), Does.Contain("No summary was written"));
    }

    [Test]
    public void Job_output_should_be_read_in_order_from_any_position()
    {
        var job = new StepJob("local-8", StepKind.Seed);
        job.AppendLine("one");
        job.AppendLine("two");
        var (first, next) = job.ReadLines(0);
        job.AppendLine("three");
        var (rest, end) = job.ReadLines(next);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(rest, Is.EqualTo(new[] { "three" }));
            Assert.That(end, Is.EqualTo(3));
            Assert.That(job.Status, Is.EqualTo(StepStatus.Running));
        });
    }

    [Test]
    public void Solution_folder_should_be_found_above_the_app_or_taken_from_settings()
    {
        var app = Directory.CreateDirectory(Path.Combine(directory, "PricingValidationFramework.ControlPanel", "bin", "Debug", "net10.0")).FullName;
        File.WriteAllText(Path.Combine(directory, "PricingValidationFramework.slnx"), "<Solution />");

        Assert.Multiple(() =>
        {
            Assert.That(new ControlPanelOptions().ResolveSolutionFolder(app), Is.EqualTo(new DirectoryInfo(directory).FullName));
            Assert.That(new ControlPanelOptions { SolutionFolder = directory }.ResolveSolutionFolder("/elsewhere"), Is.EqualTo(Path.GetFullPath(directory)));
        });
    }

    private ProcessSpec WithSummary(StepKind kind, string json)
    {
        var file = Path.Combine(directory, $"{kind}-summary.json");
        File.WriteAllText(file, json);
        return new ProcessSpec("dotnet", [], new Dictionary<string, string?>(), directory, [new StepOutputFile("Summary", file)]);
    }

    // The DataLoader command and its options, without the shared "dotnet run ... --" prefix and the --report pair.
    private static string[] Options(ProcessSpec spec)
    {
        var arguments = spec.Arguments.SkipWhile(argument => argument != "--").Skip(1).ToList();
        var report = arguments.IndexOf("--report");
        return arguments.Where((_, index) => index != report && index != report + 1).ToArray();
    }
}
