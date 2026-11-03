namespace PricingValidationFramework.ControlPanel.Steps;

using System.Globalization;

/// Where things are on this machine, all absolute.
public sealed record ControlPanelPaths(string SolutionFolder, string LocalSettings, string Configuration)
{
    public string ImportFolder => Path.Combine(SolutionFolder, "data", "import");
    public string WorkFolder => Path.Combine(SolutionFolder, "TestResults", "ControlPanel");
    public string DataLoaderProject => Path.Combine(SolutionFolder, "PricingValidationFramework.DataLoader", "PricingValidationFramework.DataLoader.csproj");
    public string TestsProject => Path.Combine(SolutionFolder, "PricingValidationFramework.Tests", "PricingValidationFramework.Tests.csproj");

    /// Where the Radar and ICE tests write Radar_<BuildId>.csv and Ice_<BuildId>.csv.
    public string ReportsFolder => Path.Combine(SolutionFolder, "PricingValidationFramework.Tests", "bin", Configuration, "net10.0", "TestResults", "Reports");
}

/// The command to run for a step. Environment values of null are removed, so a stale value in the
/// control panel's own environment can't leak into a run.
public sealed record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> Environment,
    string WorkingFolder,
    IReadOnlyList<StepOutputFile> Outputs);

public sealed record StepOutputFile(string Label, string Path);

/// Builds the same commands the VS Code launch configurations and pipelines run: the DataLoader CLI for
/// validate/import/seed/set-status, and dotnet test for the Radar and ICE tests.
public static class LocalCommandBuilder
{
    // Inputs the Radar and ICE tests read from the environment. Each run sets the ones it was given and
    // removes the rest, so the settings file's value applies instead.
    private static readonly string[] TestVariables =
    [
        "RADAR_THRESHOLD_TYPE", "RADAR_MIN_THRESHOLD", "RADAR_MAX_THRESHOLD", "RADAR_REQUEST_DATETIME",
        "TEST_TAG", "SCENARIO_TAG", "ICE_RADAR_REPORT"
    ];

    public static ProcessSpec Build(StepRequest request, ControlPanelPaths paths, string runId) => request switch
    {
        ValidateCsvRequest validate => DataLoader(paths, runId, "validate",
            ["--requests", validate.CsvPath]),
        ImportRequest import => DataLoader(paths, runId, "import",
            ["--requests", import.CsvPath, "--settings", paths.LocalSettings,
             .. Optional("--request-time", import.RequestTime),
             .. Optional("--batch-size", import.BatchSize?.ToString(CultureInfo.InvariantCulture))]),
        SeedRequest seed => DataLoader(paths, runId, "seed",
            ["--settings", paths.LocalSettings,
             .. Optional("--request-time", seed.RequestTime),
             .. Optional("--max-parallel", seed.MaxParallel?.ToString(CultureInfo.InvariantCulture))]),
        SetStatusRequest status => SetStatus(paths, runId, status),
        RadarRequest radar => Tests(paths, runId, "PricingValidationFramework.Tests.Integration.Radar.RadarValidationTests",
            new Dictionary<string, string?>
            {
                ["RADAR_THRESHOLD_TYPE"] = radar.ThresholdType?.ToString(),
                ["RADAR_MIN_THRESHOLD"] = radar.MinThreshold?.ToString(CultureInfo.InvariantCulture),
                ["RADAR_MAX_THRESHOLD"] = radar.MaxThreshold?.ToString(CultureInfo.InvariantCulture),
                ["RADAR_REQUEST_DATETIME"] = radar.RequestTime,
                ["TEST_TAG"] = radar.ProductCode,
                ["SCENARIO_TAG"] = radar.ScenarioTag
            },
            new StepOutputFile("Radar report", Path.Combine(paths.ReportsFolder, $"Radar_{runId}.csv"))),
        IceRequest ice => Tests(paths, runId, "PricingValidationFramework.Tests.Integration.Ice.IceValidationTests",
            new Dictionary<string, string?>
            {
                ["TEST_TAG"] = ice.ProductCode,
                ["SCENARIO_TAG"] = ice.ScenarioTag,
                ["ICE_RADAR_REPORT"] = ice.RadarReport
            },
            new StepOutputFile("ICE report", Path.Combine(paths.ReportsFolder, $"Ice_{runId}.csv"))),
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown step.")
    };

    private static ProcessSpec DataLoader(ControlPanelPaths paths, string runId, string command, string[] options)
    {
        var report = Path.Combine(paths.WorkFolder, $"{runId}-{command}-summary.json");
        return new ProcessSpec(
            "dotnet",
            ["run", "--project", paths.DataLoaderProject, "--configuration", paths.Configuration, "--no-launch-profile", "--",
             command, .. options, "--report", report],
            new Dictionary<string, string?>(),
            paths.SolutionFolder,
            [new StepOutputFile("Summary", report)]);
    }

    private static ProcessSpec SetStatus(ControlPanelPaths paths, string runId, SetStatusRequest request)
    {
        var markdown = Path.Combine(paths.WorkFolder, $"{runId}-set-status-summary.md");
        var spec = DataLoader(paths, runId, "set-status",
            ["--status", request.Status,
             .. Optional("--scenarios", request.ScenarioIds),
             .. Optional("--scenarios-file", request.ScenarioIdsFile),
             "--settings", paths.LocalSettings, "--markdown", markdown]);
        return spec with { Outputs = [.. spec.Outputs, new StepOutputFile("Before/after table", markdown)] };
    }

    private static ProcessSpec Tests(ControlPanelPaths paths, string runId, string fixture, Dictionary<string, string?> inputs, StepOutputFile report)
    {
        var environment = TestVariables.ToDictionary(name => name, _ => (string?)null);
        foreach (var (name, value) in inputs)
        {
            environment[name] = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        // Each run gets its own build ID, so its report doesn't overwrite the last one. The live fixtures read
        // local-settings.json in the Development environment; PVF_LOCAL_SETTINGS names the exact file.
        environment["BUILD_BUILDID"] = runId;
        environment["PVF_LOCAL_SETTINGS"] = paths.LocalSettings;
        environment["DOTNET_ENVIRONMENT"] = "Development";
        environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        return new ProcessSpec(
            "dotnet",
            ["test", paths.TestsProject, "--configuration", paths.Configuration, "--filter", $"FullyQualifiedName~{fixture}",
             "--logger", "console;verbosity=normal", "--", "NUnit.ExplicitMode=Relaxed"],
            environment,
            paths.SolutionFolder,
            [report]);
    }

    private static string[] Optional(string option, string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : [option, value];
}
