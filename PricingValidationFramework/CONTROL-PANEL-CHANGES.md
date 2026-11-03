# Control panel branch: file changes (excluding tests)

Branch `control-panel`, off `loader-values-batches-update` at `a964cfe`. Nothing is committed.

- **New files (17):** the `PricingValidationFramework.ControlPanel` project, the set-status update (`ScenarioStatusUpdater.cs`, `SetStatusCommand.cs`) and `azure-pipelines-set-status.yml`. `add-control-panel.bat` adds exactly these to an existing folder.
- **Changed files (7):** shown as diffs. `add-control-panel.bat` doesn't touch these; apply them by hand. The `Program.cs` change is required for step 6 (set status).
- **Tests (not shown):** `ControlPanelTests.cs` and `SetStatusCommandTests.cs` (new), plus additions to `DatabaseQueryContractTests.cs`. They need the `Tests.csproj` reference and the DataLoader `InternalsVisibleTo` line below.

## Summary

| # | File | Change |
|---|---|---|
| 1 | `PricingValidationFramework.ControlPanel\ControlPanelOptions.cs` | New |
| 2 | `PricingValidationFramework.ControlPanel\PricingValidationFramework.ControlPanel.csproj` | New |
| 3 | `PricingValidationFramework.ControlPanel\Program.cs` | New |
| 4 | `PricingValidationFramework.ControlPanel\Steps\IStepRunner.cs` | New |
| 5 | `PricingValidationFramework.ControlPanel\Steps\LocalCommandBuilder.cs` | New |
| 6 | `PricingValidationFramework.ControlPanel\Steps\LocalStepRunner.cs` | New |
| 7 | `PricingValidationFramework.ControlPanel\Steps\StepInputs.cs` | New |
| 8 | `PricingValidationFramework.ControlPanel\Steps\StepJob.cs` | New |
| 9 | `PricingValidationFramework.ControlPanel\Steps\StepRequests.cs` | New |
| 10 | `PricingValidationFramework.ControlPanel\Steps\StepResultReader.cs` | New |
| 11 | `PricingValidationFramework.ControlPanel\appsettings.json` | New |
| 12 | `PricingValidationFramework.ControlPanel\ui\app.js` | New |
| 13 | `PricingValidationFramework.ControlPanel\ui\index.html` | New |
| 14 | `PricingValidationFramework.ControlPanel\ui\styles.css` | New |
| 15 | `PricingValidationFramework.Core\Database\ScenarioStatusUpdater.cs` | New |
| 16 | `PricingValidationFramework.DataLoader\SetStatusCommand.cs` | New |
| 17 | `azure-pipelines-set-status.yml` | New |
| 18 | `.vscode\launch.json` | Changed |
| 19 | `.vscode\tasks.json` | Changed |
| 20 | `PricingValidationFramework.DataLoader\PricingValidationFramework.DataLoader.csproj` | Changed |
| 21 | `PricingValidationFramework.DataLoader\Program.cs` | Changed |
| 22 | `PricingValidationFramework.DataLoader\README.md` | Changed |
| 23 | `PricingValidationFramework.slnx` | Changed |
| 24 | `README.md` | Changed |

## New files

### `PricingValidationFramework.ControlPanel\ControlPanelOptions.cs`

```csharp
namespace PricingValidationFramework.ControlPanel;

/// The "ControlPanel" section of appsettings.json.
public sealed class ControlPanelOptions
{
    public int Port { get; set; } = 5080;

    /// The folder holding PricingValidationFramework.slnx. Empty: found by walking up from this app's folder.
    public string SolutionFolder { get; set; } = string.Empty;

    /// The settings file every step uses (database, Radar, ICE, thresholds), relative to SolutionFolder.
    public string LocalSettings { get; set; } = "data/import/local-settings.json";

    /// Build configuration the DataLoader and tests run in (Debug matches the VS Code launch configurations).
    public string Configuration { get; set; } = "Debug";

    public int MaxUploadMegabytes { get; set; } = 200;

    /// The absolute solution folder: the configured one, or the nearest parent of startFolder with the .slnx.
    public string ResolveSolutionFolder(string startFolder)
    {
        if (!string.IsNullOrWhiteSpace(SolutionFolder))
        {
            return Path.GetFullPath(SolutionFolder);
        }

        for (var folder = new DirectoryInfo(startFolder); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PricingValidationFramework.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            "PricingValidationFramework.slnx wasn't found above this app. Set ControlPanel:SolutionFolder in appsettings.json.");
    }
}
```

### `PricingValidationFramework.ControlPanel\PricingValidationFramework.ControlPanel.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <!--
    Local control panel: a single web page (http://localhost:5080) with a button per step - choose a
    scenarios CSV, import, seed, Radar compare, ICE check, set status. Each step runs the same command
    the VS Code launch configurations run (the DataLoader CLI or dotnet test), on this machine, with
    data/import/local-settings.json. Behind IStepRunner, so a pipeline runner can replace it later.
  -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="PricingValidationFramework.Tests" />
  </ItemGroup>

  <ItemGroup>
    <!-- The page (index.html, app.js, styles.css), served from the build output. -->
    <None Include="ui\**\*" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PricingValidationFramework.Core\PricingValidationFramework.Core.csproj" />
  </ItemGroup>
</Project>
```

### `PricingValidationFramework.ControlPanel\Program.cs`

```csharp
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using PricingValidationFramework.ControlPanel;
using PricingValidationFramework.ControlPanel.Steps;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
var options = builder.Configuration.GetSection("ControlPanel").Get<ControlPanelOptions>() ?? new ControlPanelOptions();
var solutionFolder = options.ResolveSolutionFolder(AppContext.BaseDirectory);
var paths = new ControlPanelPaths(solutionFolder, Path.GetFullPath(Path.Combine(solutionFolder, options.LocalSettings)), options.Configuration);
var maxUploadBytes = options.MaxUploadMegabytes * 1024L * 1024L;

// This machine only: the panel writes to the database, so it listens on localhost and nowhere else.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenLocalhost(options.Port);
    kestrel.Limits.MaxRequestBodySize = maxUploadBytes;
});
builder.Services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = maxUploadBytes);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton<ControlPanelState>();
builder.Services.AddSingleton<IStepRunner, LocalStepRunner>();

var app = builder.Build();

// Only this page may start steps. Other websites open in the same browser can send requests to
// localhost too; they can't add this header without the browser asking first (which nothing here
// answers), and their Origin wouldn't match.
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method))
    {
        var origin = context.Request.Headers.Origin.ToString();
        var sameOrigin = origin.Length == 0 || origin == $"http://localhost:{options.Port}" || origin == $"http://127.0.0.1:{options.Port}";
        if (!sameOrigin || context.Request.Headers["X-Control-Panel"] != "1")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    await next();
});

var ui = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "ui"));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = ui });
app.UseStaticFiles(new StaticFileOptions { FileProvider = ui });

app.MapGet("/api/state", (ControlPanelState state, IStepRunner runner) =>
{
    var (csvPath, csvName) = state.SelectedCsv;
    return Results.Ok(new
    {
        solutionFolder,
        localSettings = paths.LocalSettings,
        localSettingsFound = File.Exists(paths.LocalSettings),
        selectedCsv = csvPath is null ? null : new { name = csvName, savedAs = Path.GetRelativePath(solutionFolder, csvPath) },
        lastRadarReport = state.LastRadarReport is null ? null : Path.GetFileName(state.LastRadarReport),
        current = runner.Current?.Id
    });
});

// 1. Choose CSV: saved into data/import as the newest scenarios-{timestamp}.csv, then checked offline.
app.MapPost("/api/csv", async (IFormFile? file, ControlPanelState state, IStepRunner runner) =>
{
    if (file is null || file.Length == 0)
    {
        return Problem("Choose a CSV file.");
    }

    if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
    {
        return Problem("The file must be a .csv.");
    }

    return await StartAsync(runner, async () =>
    {
        Directory.CreateDirectory(paths.ImportFolder);
        var saved = Path.Combine(paths.ImportFolder, $"scenarios-{DateTime.Now:yyyyMMdd-HHmmss-fff}.csv");
        await using (var stream = File.Create(saved))
        {
            await file.CopyToAsync(stream);
        }

        state.SelectCsv(saved, Path.GetFileName(file.FileName));
        return new ValidateCsvRequest(saved);
    });
}).DisableAntiforgery();

// 2. Import the chosen CSV.
app.MapPost("/api/import", (ImportInput input, ControlPanelState state, IStepRunner runner) =>
    Start(runner, () =>
    {
        RequireLocalSettings();
        var (csv, _) = state.SelectedCsv;
        if (csv is null || !File.Exists(csv))
        {
            throw new StepInputException("Choose a CSV first (step 1).");
        }

        return new ImportRequest(csv, StepInputs.RequestTime(input.RequestTime), StepInputs.WholeNumber(input.BatchSize, "Batch size", 1, 1000));
    }));

// 3. Seed baselines.
app.MapPost("/api/seed", (SeedInput input, IStepRunner runner) =>
    Start(runner, () =>
    {
        RequireLocalSettings();
        return new SeedRequest(StepInputs.RequestTime(input.RequestTime), StepInputs.WholeNumber(input.MaxParallel, "Scenarios at once", 1, 16));
    }));

// 4. Radar compare.
app.MapPost("/api/radar", (RadarInput input, IStepRunner runner) =>
    Start(runner, () =>
    {
        RequireLocalSettings();
        var min = StepInputs.Threshold(input.MinThreshold, "Minimum threshold");
        var max = StepInputs.Threshold(input.MaxThreshold, "Maximum threshold");
        StepInputs.ThresholdRange(min, max);
        return new RadarRequest(StepInputs.ThresholdTypeName(input.ThresholdType), min, max,
            StepInputs.ProductCode(input.ProductCode), StepInputs.ScenarioTag(input.ScenarioTag), StepInputs.RequestTime(input.RequestTime));
    }));

// 5. ICE check.
app.MapPost("/api/ice", (IceInput input, ControlPanelState state, IStepRunner runner) =>
    Start(runner, () =>
    {
        RequireLocalSettings();
        string? report = null;
        if (input.UseLatestRadarReport)
        {
            report = state.LastRadarReport;
            if (report is null || !File.Exists(report))
            {
                throw new StepInputException("There's no Radar report from this session yet. Run step 4 first, or untick the option.");
            }
        }

        return new IceRequest(StepInputs.ProductCode(input.ProductCode), StepInputs.ScenarioTag(input.ScenarioTag), report);
    }));

// 6. Set status: IDs typed in, a file of IDs (one per line, or a CSV), or both.
app.MapPost("/api/set-status", async ([FromForm] string? status, [FromForm] string? scenarioIds, IFormFile? file, IStepRunner runner) =>
    await StartAsync(runner, async () =>
    {
        RequireLocalSettings();
        var checkedStatus = StepInputs.Status(status);
        string? idFile = null;
        if (file is { Length: > 0 })
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".csv" or ".txt"))
            {
                throw new StepInputException("The ID file must be a .csv or .txt.");
            }

            Directory.CreateDirectory(paths.WorkFolder);
            idFile = Path.Combine(paths.WorkFolder, $"status-ids-{DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");
            await using var stream = File.Create(idFile);
            await file.CopyToAsync(stream);
        }

        if (string.IsNullOrWhiteSpace(scenarioIds) && idFile is null)
        {
            throw new StepInputException("Enter scenario IDs or choose a file of IDs.");
        }

        return new SetStatusRequest(checkedStatus, string.IsNullOrWhiteSpace(scenarioIds) ? null : scenarioIds.Trim(), idFile);
    })).DisableAntiforgery();

app.MapGet("/api/jobs/{id}", (string id, int? from, IStepRunner runner) =>
{
    var job = runner.Find(id);
    if (job is null)
    {
        return Results.NotFound();
    }

    var (lines, next) = job.ReadLines(from ?? 0);
    return Results.Ok(new
    {
        job.Id,
        kind = job.Kind.ToString(),
        status = job.Status.ToString(),
        job.ExitCode,
        job.StartedAt,
        job.FinishedAt,
        job.Summary,
        outputs = job.Outputs.Select((output, index) => new { output.Label, name = Path.GetFileName(output.Path), exists = File.Exists(output.Path), index }),
        lines,
        next
    });
});

// Opens one of a finished step's own output files (summaries, reports); nothing else can be read.
app.MapGet("/api/jobs/{id}/files/{index:int}", (string id, int index, IStepRunner runner) =>
{
    var output = runner.Find(id)?.Outputs.ElementAtOrDefault(index);
    if (output is null || !File.Exists(output.Path))
    {
        return Results.NotFound();
    }

    var type = Path.GetExtension(output.Path) switch { ".json" => "application/json", ".csv" => "text/csv", _ => "text/plain" };
    return Results.File(output.Path, type, Path.GetFileName(output.Path));
});

app.MapPost("/api/jobs/cancel", (IStepRunner runner) =>
    runner.CancelCurrent() ? Results.Ok() : Problem("Nothing is running."));

// Printed (not logged) so it always shows; VS Code's launch configuration opens the browser when it sees it.
app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine(
    $"Control panel: http://localhost:{options.Port} (solution {solutionFolder}, settings {paths.LocalSettings}). Press Ctrl+C to stop."));
app.Run();

void RequireLocalSettings()
{
    if (!File.Exists(paths.LocalSettings))
    {
        throw new StepInputException(
            $"Settings file not found: {paths.LocalSettings}. Copy data/import/examples/local-settings.example.json there and fill it in.");
    }
}

static IResult Start(IStepRunner runner, Func<StepRequest> buildRequest)
{
    try
    {
        var job = runner.Start(buildRequest());
        return Results.Accepted($"/api/jobs/{job.Id}", new { job.Id });
    }
    catch (StepInputException exception)
    {
        return Problem(exception.Message);
    }
    catch (StepRunnerBusyException exception)
    {
        return Results.Conflict(new { error = exception.Message });
    }
}

static async Task<IResult> StartAsync(IStepRunner runner, Func<Task<StepRequest>> buildRequest)
{
    // Refuse before saving any upload if a step is already running.
    if (runner.Current is { } running)
    {
        return Results.Conflict(new { error = new StepRunnerBusyException(running).Message });
    }

    try
    {
        var request = await buildRequest();
        return Start(runner, () => request);
    }
    catch (StepInputException exception)
    {
        return Problem(exception.Message);
    }
}

static IResult Problem(string message) => Results.BadRequest(new { error = message });

internal sealed record ImportInput(string? RequestTime, string? BatchSize);
internal sealed record SeedInput(string? RequestTime, string? MaxParallel);
internal sealed record RadarInput(string? ThresholdType, string? MinThreshold, string? MaxThreshold, string? ProductCode, string? ScenarioTag, string? RequestTime);
internal sealed record IceInput(string? ProductCode, string? ScenarioTag, bool UseLatestRadarReport);
```

### `PricingValidationFramework.ControlPanel\Steps\IStepRunner.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

/// Runs one step at a time. LocalStepRunner runs the commands on this machine; a pipeline runner can
/// implement the same interface later (queue the Azure DevOps pipeline with the request's values), and
/// the page and endpoints stay as they are.
public interface IStepRunner
{
    /// The step currently running, if any.
    StepJob? Current { get; }

    /// Starts a step. Throws StepRunnerBusyException while another step is running.
    StepJob Start(StepRequest request);

    StepJob? Find(string id);

    /// Stops the running step. False if nothing is running.
    bool CancelCurrent();
}

public sealed class StepRunnerBusyException(StepJob running)
    : Exception($"A step is already running ({running.Kind}). Wait for it to finish, or cancel it.");

/// What the page remembers between steps: the CSV chosen in step 1 and the latest Radar report.
public sealed class ControlPanelState
{
    private readonly object gate = new();
    private string? selectedCsv;
    private string? selectedCsvName;
    private string? lastRadarReport;

    public (string? Path, string? OriginalName) SelectedCsv
    {
        get { lock (gate) { return (selectedCsv, selectedCsvName); } }
    }

    public string? LastRadarReport
    {
        get { lock (gate) { return lastRadarReport; } }
        set { lock (gate) { lastRadarReport = value; } }
    }

    public void SelectCsv(string path, string originalName)
    {
        lock (gate)
        {
            selectedCsv = path;
            selectedCsvName = originalName;
        }
    }
}
```

### `PricingValidationFramework.ControlPanel\Steps\LocalCommandBuilder.cs`

```csharp
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
```

### `PricingValidationFramework.ControlPanel\Steps\LocalStepRunner.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

using System.Collections.Concurrent;
using System.Diagnostics;

/// Runs each step as a command on this machine (the DataLoader CLI or dotnet test), one at a time,
/// capturing its output for the page.
public sealed class LocalStepRunner(ControlPanelPaths paths, ControlPanelState state) : IStepRunner, IDisposable
{
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, StepJob> jobs = new(StringComparer.Ordinal);
    private StepJob? current;
    private Process? currentProcess;

    public StepJob? Current
    {
        get { lock (gate) { return current?.Status == StepStatus.Running ? current : null; } }
    }

    public StepJob Start(StepRequest request)
    {
        lock (gate)
        {
            if (current?.Status == StepStatus.Running)
            {
                throw new StepRunnerBusyException(current);
            }

            // e.g. local-20261021-093000-123: unique per run, and safe in file names and as a build ID.
            var runId = $"local-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
            var spec = LocalCommandBuilder.Build(request, paths, runId);
            var job = new StepJob(runId, request.Kind) { Outputs = spec.Outputs };
            jobs[job.Id] = job;
            current = job;
            _ = Task.Run(() => RunAsync(job, spec));
            return job;
        }
    }

    public StepJob? Find(string id) => jobs.TryGetValue(id, out var job) ? job : null;

    public bool CancelCurrent()
    {
        lock (gate)
        {
            if (current?.Status != StepStatus.Running || currentProcess is null)
            {
                return false;
            }

            try
            {
                currentProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            current.AppendLine("Cancelled from the control panel.");
            current.Finish(StepStatus.Cancelled, null, ["Cancelled. Anything already committed to the database stays; check the summary before running it again."]);
            return true;
        }
    }

    private async Task RunAsync(StepJob job, ProcessSpec spec)
    {
        var output = new ConcurrentQueue<string>();
        try
        {
            Directory.CreateDirectory(paths.WorkFolder);
            var startInfo = new ProcessStartInfo(spec.FileName)
            {
                WorkingDirectory = spec.WorkingFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Arguments are passed as a list, never through a shell, so typed values can't run as commands.
            foreach (var argument in spec.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in spec.Environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(name);
                }
                else
                {
                    startInfo.Environment[name] = value;
                }
            }

            job.AppendLine($"> {spec.FileName} {string.Join(' ', spec.Arguments.Select(Quote))}");
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.Enqueue(e.Data); job.AppendLine(e.Data); } };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { output.Enqueue(e.Data); job.AppendLine(e.Data); } };

            lock (gate)
            {
                process.Start();
                currentProcess = process;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            lock (gate)
            {
                currentProcess = null;
                if (job.Status == StepStatus.Cancelled)
                {
                    return;
                }
            }

            if (job.Kind == StepKind.Radar && spec.Outputs.FirstOrDefault() is { } report && File.Exists(report.Path))
            {
                state.LastRadarReport = report.Path;
            }

            var summary = StepResultReader.Summarize(job.Kind, spec, output.ToArray());
            job.Finish(process.ExitCode == 0 ? StepStatus.Succeeded : StepStatus.Failed, process.ExitCode, summary);
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                currentProcess = null;
            }

            job.AppendLine(exception.ToString());
            job.Finish(StepStatus.Failed, null, [$"The step couldn't run ({exception.GetType().Name}): {exception.Message}"]);
        }
    }

    private static string Quote(string argument) => argument.Contains(' ') ? $"\"{argument}\"" : argument;

    public void Dispose() => CancelCurrent();
}
```

### `PricingValidationFramework.ControlPanel\Steps\StepInputs.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

using System.Globalization;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Validation;

/// Turns what was typed on the page into checked values, using the same rules as the commands, so a
/// mistake is reported on the page straight away rather than part-way through a run. Blank means "not given".
/// Every method throws StepInputException with a message to show next to the form.
public static class StepInputs
{
    public static string? RequestTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            RequestTimeFormatter.Resolve(value);
            return value.Trim();
        }
        catch (ArgumentException)
        {
            throw new StepInputException("Request time must be 'now' or yyyy-MM-ddZHH:mm:ss, e.g. 2026-10-21Z09:00:00.");
        }
    }

    public static int? WholeNumber(string? value, string label, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max
            ? number
            : throw new StepInputException($"{label} must be a whole number from {min} to {max}.");
    }

    public static decimal? Threshold(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Float, like the Radar tests: a '.' decimal point and no thousands separators, so "0,5" is rejected
        // rather than read as 5.
        return decimal.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new StepInputException($"{label} must be a number with a '.' decimal point, e.g. -0.5 or 2.");
    }

    public static void ThresholdRange(decimal? min, decimal? max)
    {
        if (min > max)
        {
            throw new StepInputException("The minimum threshold must not be more than the maximum.");
        }
    }

    public static ThresholdType? ThresholdTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var name = Enum.GetNames<ThresholdType>().FirstOrDefault(name => name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return name is not null ? Enum.Parse<ThresholdType>(name) : throw new StepInputException("Threshold type must be Pound or Percentage.");
    }

    public static string? ProductCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 100 && !trimmed.Any(char.IsControl)
            ? trimmed
            : throw new StepInputException("Product code must be at most 100 characters on one line.");
    }

    public static string? ScenarioTag(string? value)
    {
        try
        {
            var tag = ScenarioTags.ValidateFilter(value, "Scenario tag");
            return tag.Length == 0 ? null : tag;
        }
        catch (InvalidOperationException exception)
        {
            throw new StepInputException(exception.Message);
        }
    }

    public static string Status(string? value)
    {
        var status = value?.Trim().ToUpperInvariant();
        return status is "PASS" or "FAIL" ? status : throw new StepInputException("Choose PASS or FAIL.");
    }
}

public sealed class StepInputException(string message) : Exception(message);
```

### `PricingValidationFramework.ControlPanel\Steps\StepJob.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<StepStatus>))]
public enum StepStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// One run of a step: its progress, output and result. Safe to read while the step is running.
public sealed class StepJob(string id, StepKind kind)
{
    public const int MaxLogLines = 20000;

    private readonly object gate = new();
    private readonly List<string> log = [];
    private int droppedLines;

    public string Id { get; } = id;
    public StepKind Kind { get; } = kind;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public DateTimeOffset? FinishedAt { get; private set; }
    public StepStatus Status { get; private set; } = StepStatus.Running;
    public int? ExitCode { get; private set; }
    public IReadOnlyList<string> Summary { get; private set; } = [];
    public IReadOnlyList<StepOutputFile> Outputs { get; set; } = [];

    public void AppendLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            if (log.Count == MaxLogLines)
            {
                // Keep the latest output; very long runs drop their earliest lines.
                log.RemoveAt(0);
                droppedLines++;
            }

            log.Add(line);
        }
    }

    /// Lines from position `from` (counting every line ever written), and the position to ask for next.
    public (IReadOnlyList<string> Lines, int Next) ReadLines(int from)
    {
        lock (gate)
        {
            var start = Math.Max(0, from - droppedLines);
            return (log.Skip(start).ToArray(), droppedLines + log.Count);
        }
    }

    public void Finish(StepStatus status, int? exitCode, IReadOnlyList<string> summary)
    {
        lock (gate)
        {
            Status = status;
            ExitCode = exitCode;
            Summary = summary;
            FinishedAt = DateTimeOffset.Now;
        }
    }
}
```

### `PricingValidationFramework.ControlPanel\Steps\StepRequests.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

using PricingValidationFramework.Core.Models.Enums;

public enum StepKind
{
    ValidateCsv,
    Import,
    Seed,
    Radar,
    Ice,
    SetStatus
}

/// What a step should do, independent of how it runs (locally now, a pipeline later). Values are already
/// checked by StepInputs; null means "not given", so the settings file or the command's default applies.
public abstract record StepRequest(StepKind Kind);

/// 1. Check a chosen scenarios CSV offline (no database).
public sealed record ValidateCsvRequest(string CsvPath) : StepRequest(StepKind.ValidateCsv);

/// 2. Import the chosen CSV into [Xml].[Request].
public sealed record ImportRequest(string CsvPath, string? RequestTime, int? BatchSize) : StepRequest(StepKind.Import);

/// 3. Seed first baselines into [Xml].[Response] for scenarios that have none.
public sealed record SeedRequest(string? RequestTime, int? MaxParallel) : StepRequest(StepKind.Seed);

/// 4. Radar compare: the Radar tests, with the pipeline's inputs.
public sealed record RadarRequest(
    ThresholdType? ThresholdType,
    decimal? MinThreshold,
    decimal? MaxThreshold,
    string? ProductCode,
    string? ScenarioTag,
    string? RequestTime) : StepRequest(StepKind.Radar);

/// 5. ICE check: the ICE tests. With RadarReport, exactly the scenarios that passed in that Radar run;
/// otherwise the newest PASS baseline per product/scheme within the filters.
public sealed record IceRequest(string? ProductCode, string? ScenarioTag, string? RadarReport) : StepRequest(StepKind.Ice);

/// 6. Set [Xml].[Response].Status to PASS or FAIL for the listed scenario IDs and/or a file of IDs.
public sealed record SetStatusRequest(string Status, string? ScenarioIds, string? ScenarioIdsFile) : StepRequest(StepKind.SetStatus);
```

### `PricingValidationFramework.ControlPanel\Steps\StepResultReader.cs`

```csharp
namespace PricingValidationFramework.ControlPanel.Steps;

using System.Text.Json;
using System.Text.RegularExpressions;

/// Turns a finished step's summary file or test output into a few lines for the page.
public static partial class StepResultReader
{
    // The one-line totals ("Failed!  - Failed: 2, Passed: 8, Skipped: 0, Total: 10, ...")...
    [GeneratedRegex(@"(?:Passed|Failed)!\s+-\s+Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+)")]
    private static partial Regex TestTotals();

    // ...or, with the detailed console logger, one count per line after "Total tests: 10".
    [GeneratedRegex(@"^\s*(?<name>Total tests|Passed|Failed|Skipped):\s+(?<count>\d+)\s*$")]
    private static partial Regex TestCountLine();

    public static IReadOnlyList<string> Summarize(StepKind kind, ProcessSpec spec, IReadOnlyList<string> output)
    {
        if (kind is StepKind.Radar or StepKind.Ice)
        {
            var totals = output.Select(line => TestTotals().Match(line)).LastOrDefault(match => match.Success);
            if (totals is not null)
            {
                return [$"Scenarios: {totals.Groups["total"].Value}. Passed {totals.Groups["passed"].Value}, failed or errored {totals.Groups["failed"].Value}."];
            }

            var start = output.ToList().FindLastIndex(line => TestCountLine().Match(line) is { Success: true } match && match.Groups["name"].Value == "Total tests");
            if (start < 0)
            {
                return ["No test results were reported. See the output for build or setup errors."];
            }

            var counts = output.Skip(start).Select(line => TestCountLine().Match(line)).Where(match => match.Success)
                .GroupBy(match => match.Groups["name"].Value).ToDictionary(group => group.Key, group => group.First().Groups["count"].Value);
            return [$"Scenarios: {counts["Total tests"]}. Passed {counts.GetValueOrDefault("Passed", "0")}, failed or errored {counts.GetValueOrDefault("Failed", "0")}."];
        }

        var summaryFile = spec.Outputs.FirstOrDefault()?.Path;
        if (summaryFile is null || !File.Exists(summaryFile))
        {
            return ["No summary was written. See the output for what went wrong."];
        }

        using var json = JsonDocument.Parse(File.ReadAllText(summaryFile));
        var root = json.RootElement;
        var lines = kind switch
        {
            StepKind.ValidateCsv => new List<string> { $"File: {Text(root, "RequestFile")}", $"Requests read: {Number(root, "InputRequests")}." },
            StepKind.Import => [$"File: {Text(root, "RequestFile")}",
                $"Requests read {Number(root, "InputRequests")}: inserted {Number(root, "InsertedRequests")}, updated {Number(root, "UpdatedRequests")}, unchanged {Number(root, "SkippedRequests")}."],
            StepKind.Seed => [$"Request time {Text(root, "RequestTime")}. Scenarios without a baseline: {Number(root, "ScenariosWithoutBaseline")}.",
                $"Seeded {Number(root, "Seeded")}, already existed {Number(root, "AlreadyExisted")}, failed {Number(root, "Failed")}, not run {Number(root, "NotRun")}."],
            StepKind.SetStatus => [$"Set to {Text(root, "Status")}, recorded as {Text(root, "ChangedBy")}.",
                $"Updated {Number(root, "Updated")}, already {Text(root, "Status")} {Number(root, "AlreadySet")}, not found {Number(root, "NotFound")}."],
            _ => []
        };

        // Problems: the first few, each with its row or scenario.
        if (root.TryGetProperty("Issues", out var issues) && issues.ValueKind == JsonValueKind.Array)
        {
            var all = issues.EnumerateArray().ToArray();
            lines.AddRange(all.Take(10).Select(issue => issue.ValueKind == JsonValueKind.String
                ? $"Problem: {issue.GetString()}"
                : $"Problem{RowLabel(issue)}: {Text(issue, "Reason")}"));
            if (all.Length > 10)
            {
                lines.Add($"... and {all.Length - 10} more problem(s) in the summary file.");
            }
        }

        if (kind == StepKind.Seed && root.TryGetProperty("Results", out var results))
        {
            foreach (var failed in results.EnumerateArray().Where(result => Text(result, "Outcome") == "Failed").Take(10))
            {
                lines.Add($"Not seeded: {Text(failed, "ScenarioId")} - {Text(failed, "Stage")}: {Text(failed, "Error")}");
            }
        }

        if (kind == StepKind.SetStatus && root.TryGetProperty("Results", out var changes))
        {
            foreach (var missing in changes.EnumerateArray().Where(change => Text(change, "Outcome") == "NotFound").Take(10))
            {
                lines.Add($"Not found: {Text(missing, "ScenarioId")}");
            }
        }

        return lines;
    }

    private static string RowLabel(JsonElement issue)
    {
        var record = issue.TryGetProperty("RecordNumber", out var number) && number.TryGetInt32(out var value) && value > 0 ? $" in row {value}" : string.Empty;
        var scenario = Text(issue, "ScenarioId");
        return record + (scenario.Length > 0 ? $" ({scenario})" : string.Empty);
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetRawText() : "?";
}
```

### `PricingValidationFramework.ControlPanel\appsettings.json`

```json
{
  "ControlPanel": {
    "Port": 5080,
    "SolutionFolder": "",
    "LocalSettings": "data/import/local-settings.json",
    "Configuration": "Debug",
    "MaxUploadMegabytes": 200
  },
  "AllowedHosts": "localhost;127.0.0.1",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

### `PricingValidationFramework.ControlPanel\ui\app.js`

```javascript
// Control panel page: starts steps through the local API and follows their output.
const $ = (id) => document.getElementById(id);
const forms = ['csvForm', 'importForm', 'seedForm', 'radarForm', 'iceForm', 'statusForm'];
const stepNames = {
  ValidateCsv: '1. Check CSV', Import: '2. Import', Seed: '3. Seed baselines',
  Radar: '4. Radar compare', Ice: '5. ICE check', SetStatus: '6. Set status'
};
let jobId = null;
let nextLine = 0;
let pollTimer = null;

// Every request carries this header; the server refuses changes without it (see Program.cs).
async function api(path, options = {}) {
  const response = await fetch(path, { ...options, headers: { 'X-Control-Panel': '1', ...(options.headers || {}) } });
  const body = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  if (!response.ok) {
    throw new Error(body?.error || `The request failed (${response.status}).`);
  }
  return body;
}

function showError(message) {
  $('formError').textContent = message;
  $('formError').hidden = !message;
}

function setBusy(busy) {
  for (const id of forms) {
    $(id).querySelector('button[type="submit"]').disabled = busy;
  }
  $('cancelButton').hidden = !busy;
}

async function refreshState() {
  try {
    const state = await api('/api/state');
    $('settingsPath').textContent = state.localSettings;
    const lines = [
      state.localSettingsFound
        ? '<span class="ok">Settings file found</span>'
        : '<span class="bad">Settings file missing: copy data/import/examples/local-settings.example.json to data/import/local-settings.json</span>',
      state.selectedCsv ? `CSV: ${escapeHtml(state.selectedCsv.name)} (saved as ${escapeHtml(state.selectedCsv.savedAs)})` : 'CSV: none chosen yet',
      state.lastRadarReport ? `Latest Radar report: ${escapeHtml(state.lastRadarReport)}` : 'Latest Radar report: none this session'
    ];
    $('state').innerHTML = lines.map((line) => `<span>${line}</span>`).join('');
    $('useLatestRadarReport').disabled = !state.lastRadarReport;
    if (state.current && state.current !== jobId) {
      follow(state.current);
    }
  } catch (error) {
    $('state').textContent = error.message;
  }
}

function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

function follow(id) {
  jobId = id;
  nextLine = 0;
  $('log').textContent = '';
  $('summary').replaceChildren();
  $('files').replaceChildren();
  setBusy(true);
  clearTimeout(pollTimer);
  poll();
}

async function poll() {
  try {
    const job = await api(`/api/jobs/${encodeURIComponent(jobId)}?from=${nextLine}`);
    $('jobTitle').textContent = `${stepNames[job.kind] || job.kind} · ${job.id}`;
    $('jobStatus').hidden = false;
    $('jobStatus').textContent = job.status;
    $('jobStatus').className = `badge ${job.status}`;
    if (job.lines.length) {
      const log = $('log');
      const atBottom = log.scrollTop + log.clientHeight >= log.scrollHeight - 20;
      log.textContent += job.lines.join('\n') + '\n';
      if (atBottom) log.scrollTop = log.scrollHeight;
    }
    nextLine = job.next;

    if (job.status === 'Running') {
      pollTimer = setTimeout(poll, 1000);
      return;
    }

    $('summary').replaceChildren(...job.summary.map((line) => Object.assign(document.createElement('li'), { textContent: line })));
    $('files').replaceChildren(...job.outputs.filter((output) => output.exists).map((output) => {
      const item = document.createElement('li');
      const link = Object.assign(document.createElement('a'), {
        href: `/api/jobs/${encodeURIComponent(job.id)}/files/${output.index}`, textContent: `${output.label}: ${output.name}`, target: '_blank', rel: 'noopener'
      });
      item.append(link);
      return item;
    }));
    setBusy(false);
    refreshState();
  } catch (error) {
    showError(error.message);
    setBusy(false);
  }
}

async function start(path, body) {
  showError('');
  try {
    const isForm = body instanceof FormData;
    const result = await api(path, {
      method: 'POST',
      body: isForm ? body : JSON.stringify(body),
      headers: isForm ? {} : { 'Content-Type': 'application/json' }
    });
    follow(result.id);
  } catch (error) {
    showError(error.message);
  }
}

const values = (form) => Object.fromEntries(new FormData(form).entries());

$('csvForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/csv', new FormData(event.target));
});
$('importForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/import', values(event.target));
});
$('seedForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/seed', values(event.target));
});
$('radarForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/radar', values(event.target));
});
$('iceForm').addEventListener('submit', (event) => {
  event.preventDefault();
  const input = values(event.target);
  start('/api/ice', { productCode: input.productCode, scenarioTag: input.scenarioTag, useLatestRadarReport: $('useLatestRadarReport').checked });
});
$('statusForm').addEventListener('submit', (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  const count = (form.get('scenarioIds') || '').split(/[,;\n]/).filter((id) => id.trim()).length;
  const file = form.get('file');
  if (!file || !file.size) form.delete('file');
  const what = count ? `${count} scenario ID(s)` : 'the IDs in the chosen file';
  const confirmPanel = $('statusForm').querySelector('.confirm');
  if (!confirmPanel) {
    // Ask once on the page before changing the database (browser confirm() boxes aren't used).
    const panel = document.createElement('p');
    panel.className = 'error confirm';
    panel.textContent = `Set ${what} to ${form.get('status')}? Click "Set status" again to confirm.`;
    $('statusForm').querySelector('button[type="submit"]').before(panel);
    return;
  }
  confirmPanel.remove();
  start('/api/set-status', form);
});
$('statusForm').addEventListener('input', () => $('statusForm').querySelector('.confirm')?.remove());

$('cancelButton').addEventListener('click', async () => {
  try {
    await api('/api/jobs/cancel', { method: 'POST' });
  } catch (error) {
    showError(error.message);
  }
});

refreshState();
```

### `PricingValidationFramework.ControlPanel\ui\index.html`

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Pricing Validation Control Panel</title>
<link rel="stylesheet" href="styles.css">
</head>
<body>
<header class="top">
  <div>
    <h1>Pricing Validation Control Panel</h1>
    <p class="muted">Runs each step on this machine with <code id="settingsPath">data/import/local-settings.json</code>. One step runs at a time.</p>
  </div>
  <div class="state" id="state" aria-live="polite">Loading…</div>
</header>

<main class="layout">
  <section class="steps" aria-label="Steps">

    <form class="card" id="csvForm">
      <h2><span class="num">1</span> Choose scenarios CSV</h2>
      <p class="muted">The CSV saved by the scenario loader (<code>scenario_id,xml</code>). It's copied into <code>data/import</code> and checked; nothing is written to the database.</p>
      <label for="csvFile">CSV file</label>
      <input type="file" id="csvFile" name="file" accept=".csv" required>
      <button type="submit">Check CSV</button>
    </form>

    <form class="card" id="importForm">
      <h2><span class="num">2</span> Import into [Xml].[Request]</h2>
      <p class="muted">Imports the CSV chosen in step 1: new scenarios are added, changed XML is updated.</p>
      <div class="row">
        <div><label for="importRequestTime">Request time</label><input id="importRequestTime" name="requestTime" placeholder="now"></div>
        <div><label for="importBatchSize">Batch size</label><input id="importBatchSize" name="batchSize" inputmode="numeric" placeholder="100"></div>
      </div>
      <button type="submit">Import</button>
    </form>

    <form class="card" id="seedForm">
      <h2><span class="num">3</span> Seed baselines into [Xml].[Response]</h2>
      <p class="muted">Calls Radar for every scenario with no baseline and stores the response as its PASS baseline. Existing baselines are left alone.</p>
      <div class="row">
        <div><label for="seedRequestTime">Request time</label><input id="seedRequestTime" name="requestTime" placeholder="now"></div>
        <div><label for="seedMaxParallel">Scenarios at once</label><input id="seedMaxParallel" name="maxParallel" inputmode="numeric" placeholder="4"></div>
      </div>
      <button type="submit">Seed baselines</button>
    </form>

    <form class="card" id="radarForm">
      <h2><span class="num">4</span> Radar compare</h2>
      <p class="muted">Calls Radar and compares each scenario with its baseline. A PASS updates the baseline; a FAIL sets its status to FAIL. Leave a box empty to use the value in the settings file.</p>
      <div class="row">
        <div>
          <label for="thresholdType">Threshold type</label>
          <select id="thresholdType" name="thresholdType">
            <option value="">From settings (Pound if not set)</option>
            <option value="Pound">Pound (£ difference)</option>
            <option value="Percentage">Percentage (% change)</option>
          </select>
        </div>
        <div><label for="minThreshold">Minimum</label><input id="minThreshold" name="minThreshold" inputmode="decimal" placeholder="e.g. -0.50"></div>
        <div><label for="maxThreshold">Maximum</label><input id="maxThreshold" name="maxThreshold" inputmode="decimal" placeholder="e.g. 0.50"></div>
      </div>
      <div class="row">
        <div><label for="radarProduct">Product code</label><input id="radarProduct" name="productCode" placeholder="All products"></div>
        <div><label for="radarTag">Scenario tag</label><input id="radarTag" name="scenarioTag" placeholder="All tags"></div>
        <div><label for="radarRequestTime">Request time</label><input id="radarRequestTime" name="requestTime" placeholder="now"></div>
      </div>
      <button type="submit">Run Radar compare</button>
    </form>

    <form class="card" id="iceForm">
      <h2><span class="num">5</span> ICE check</h2>
      <p class="muted">Calls ICE and checks its premium equals the baseline: the newest PASS scenario per product and scheme, or exactly the scenarios that passed in the latest Radar run.</p>
      <div class="row">
        <div><label for="iceProduct">Product code</label><input id="iceProduct" name="productCode" placeholder="All products"></div>
        <div><label for="iceTag">Scenario tag</label><input id="iceTag" name="scenarioTag" placeholder="All tags"></div>
      </div>
      <label class="check"><input type="checkbox" id="useLatestRadarReport" name="useLatestRadarReport"> Only the scenarios that passed in the latest Radar run (filters ignored)</label>
      <button type="submit">Run ICE check</button>
    </form>

    <form class="card" id="statusForm">
      <h2><span class="num">6</span> Set status</h2>
      <p class="muted">Sets the baseline status of every listed scenario. Only the status changes; the baseline XML is kept, so it can be set back.</p>
      <div class="row">
        <div>
          <label for="status">New status</label>
          <select id="status" name="status" required>
            <option value="PASS">PASS</option>
            <option value="FAIL">FAIL</option>
          </select>
        </div>
        <div><label for="statusFile">File of IDs (optional)</label><input type="file" id="statusFile" name="file" accept=".csv,.txt"></div>
      </div>
      <label for="scenarioIds">Scenario IDs (comma-separated or one per line)</label>
      <textarea id="scenarioIds" name="scenarioIds" rows="3" placeholder="Carrot_CarrotTD_54798554, Carrot_CarrotTD_99185883"></textarea>
      <button type="submit">Set status</button>
    </form>
  </section>

  <section class="output" aria-label="Output">
    <div class="output-head">
      <h2 id="jobTitle">No step run yet</h2>
      <span class="badge" id="jobStatus" hidden></span>
      <button type="button" class="secondary" id="cancelButton" hidden>Cancel</button>
    </div>
    <p class="error" id="formError" role="alert" hidden></p>
    <ul class="summary" id="summary"></ul>
    <ul class="files" id="files"></ul>
    <pre class="log" id="log" aria-label="Command output"></pre>
  </section>
</main>
<script src="app.js"></script>
</body>
</html>
```

### `PricingValidationFramework.ControlPanel\ui\styles.css`

```css
:root {
  --bg: #f4f6f8;
  --surface: #ffffff;
  --fg: #1c2530;
  --muted: #5b6878;
  --rule: #d9dfe6;
  --accent: #1f5fbf;
  --accent-fg: #ffffff;
  --ok: #1d7a43;
  --ok-soft: #e3f3e9;
  --bad: #b3261e;
  --bad-soft: #fbe7e5;
  --run: #8a5a00;
  --run-soft: #fbf0d6;
  --log-bg: #0f1720;
  --log-fg: #d6dee8;
  color-scheme: light;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #11161c; --surface: #19212a; --fg: #e3e9ef; --muted: #98a6b5; --rule: #2c3743;
    --accent: #6ea2f0; --accent-fg: #0d1520; --ok: #72d39a; --ok-soft: #163222; --bad: #f19a92; --bad-soft: #3b1b19;
    --run: #e8bf68; --run-soft: #33290f; --log-bg: #0a0f14; --log-fg: #cdd6e0;
    color-scheme: dark;
  }
}

* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--fg); font: 15px/1.5 "Segoe UI", system-ui, -apple-system, Arial, sans-serif; }
code { font-family: Consolas, "SF Mono", Menlo, monospace; font-size: 0.9em; }
h1 { font-size: 1.35rem; margin: 0; }
h2 { font-size: 1.02rem; margin: 0; display: flex; align-items: center; gap: 8px; }
p { margin: 0; }
.muted { color: var(--muted); font-size: 0.9rem; }

.top { display: flex; flex-wrap: wrap; gap: 12px 24px; justify-content: space-between; align-items: flex-start; padding: 18px 20px; background: var(--surface); border-bottom: 1px solid var(--rule); }
.state { display: grid; gap: 2px; font-size: 0.88rem; text-align: right; }
.state .bad { color: var(--bad); font-weight: 600; }
.state .ok { color: var(--ok); font-weight: 600; }

.layout { display: grid; grid-template-columns: minmax(340px, 520px) 1fr; gap: 18px; padding: 18px 20px; align-items: start; }
@media (max-width: 960px) { .layout { grid-template-columns: 1fr; } }
.steps { display: grid; gap: 14px; }

.card { background: var(--surface); border: 1px solid var(--rule); border-radius: 8px; padding: 14px 16px; display: grid; gap: 10px; }
.num { display: inline-grid; place-items: center; width: 1.6rem; height: 1.6rem; border-radius: 50%; background: var(--accent); color: var(--accent-fg); font-size: 0.85rem; font-weight: 700; }
.row { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; }
.row > div { display: grid; gap: 4px; min-width: 0; }
label { font-size: 0.85rem; font-weight: 600; }
label.check { font-weight: 400; display: flex; gap: 8px; align-items: flex-start; }
input, select, textarea { font: inherit; color: inherit; background: var(--bg); border: 1px solid var(--rule); border-radius: 6px; padding: 6px 8px; width: 100%; min-width: 0; }
input[type="checkbox"] { width: auto; margin-top: 4px; }
input[type="file"] { padding: 4px; }
button { font: inherit; font-weight: 600; border: 0; border-radius: 6px; padding: 8px 14px; background: var(--accent); color: var(--accent-fg); cursor: pointer; justify-self: start; }
button.secondary { background: transparent; color: var(--bad); border: 1px solid var(--bad); }
button:disabled { opacity: 0.5; cursor: not-allowed; }
:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }

.output { background: var(--surface); border: 1px solid var(--rule); border-radius: 8px; padding: 14px 16px; display: grid; gap: 10px; position: sticky; top: 12px; min-width: 0; }
.output-head { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
.badge { font-size: 0.8rem; font-weight: 700; padding: 2px 10px; border-radius: 999px; }
.badge.Running { color: var(--run); background: var(--run-soft); }
.badge.Succeeded { color: var(--ok); background: var(--ok-soft); }
.badge.Failed, .badge.Cancelled { color: var(--bad); background: var(--bad-soft); }
.error { color: var(--bad); background: var(--bad-soft); border-radius: 6px; padding: 8px 10px; }
.summary, .files { margin: 0; padding-left: 1.1em; display: grid; gap: 4px; }
.summary:empty, .files:empty { display: none; }
.files a { color: var(--accent); }
.log { margin: 0; background: var(--log-bg); color: var(--log-fg); border-radius: 6px; padding: 10px 12px; font: 12.5px/1.45 Consolas, "SF Mono", Menlo, monospace; height: 52vh; overflow: auto; white-space: pre-wrap; word-break: break-word; }
```

### `PricingValidationFramework.Core\Database\ScenarioStatusUpdater.cs`

```csharp
namespace PricingValidationFramework.Core.Database;

using System.Data;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;

public interface IScenarioStatusUpdater
{
	Task<IReadOnlyList<ScenarioStatusChange>> SetStatusAsync(
		IReadOnlyCollection<string> scenarioIds,
		string status,
		CancellationToken cancellationToken = default);
}

/// Sets [Xml].[Response].Status to PASS or FAIL for given scenarios, e.g. after someone has reviewed a
/// FAIL. Changes Status and the row_modified* audit columns only: never XML_Response (the baseline),
/// never Last_updated (ICE picks its baseline by it), and never inserts or deletes rows.
public sealed class ScenarioStatusUpdater : IScenarioStatusUpdater
{
	public static readonly IReadOnlySet<string> AllowedStatuses = new HashSet<string>(StringComparer.Ordinal) { "PASS", "FAIL" };

	// IN @ScenarioIds becomes one parameter per ID; stay well under SQL Server's 2,100.
	internal const int MaxScenarioIdsPerStatement = 1000;

	// Read the current status first (locked for the transaction) so each ID can be reported as
	// updated, already at that status, or not found.
	private const string ReadStatusSql = """
		SELECT Scenario_id AS ScenarioId, Status
		FROM [Xml].[Response] WITH (UPDLOCK, HOLDLOCK)
		WHERE Scenario_id IN @ScenarioIds;
		""";

	// Rows already at the requested status are left alone, so their audit columns keep the last real change.
	private const string UpdateStatusSql = """
		UPDATE [Xml].[Response]
		SET Status = @Status,
		    row_modifieddate = SYSUTCDATETIME(),
		    row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())
		WHERE Scenario_id IN @ScenarioIds
		  AND (Status IS NULL OR Status <> @Status);
		""";

	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	public ScenarioStatusUpdater(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task<IReadOnlyList<ScenarioStatusChange>> SetStatusAsync(
		IReadOnlyCollection<string> scenarioIds,
		string status,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenarioIds);
		if (!AllowedStatuses.Contains(status))
		{
			throw new ArgumentException("Status must be PASS or FAIL.", nameof(status));
		}

		var changes = new List<ScenarioStatusChange>(scenarioIds.Count);
		foreach (var chunk in scenarioIds.Chunk(MaxScenarioIdsPerStatement))
		{
			changes.AddRange(await SetChunkAsync(chunk, status, cancellationToken));
		}

		return changes;
	}

	// One transaction per chunk: a chunk is either fully applied or not at all, and is safe to retry.
	private async Task<IReadOnlyList<ScenarioStatusChange>> SetChunkAsync(
		string[] scenarioIds,
		string status,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = await connectionFactory.OpenAsync(cancellationToken);
				await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
				var current = (await connection.QueryAsync<(string ScenarioId, string? Status)>(new CommandDefinition(
						ReadStatusSql, new { ScenarioIds = scenarioIds }, transaction, cancellationToken: cancellationToken)))
					.ToDictionary(row => row.ScenarioId, row => row.Status, StringComparer.OrdinalIgnoreCase);
				await connection.ExecuteAsync(new CommandDefinition(
					UpdateStatusSql,
					new { ScenarioIds = scenarioIds, Status = status, connectionFactory.AuditUser },
					transaction,
					cancellationToken: cancellationToken));
				await transaction.CommitAsync(cancellationToken);

				return scenarioIds.Select(id => !current.TryGetValue(id, out var previous)
						? new ScenarioStatusChange(id, null, status, ScenarioStatusOutcome.NotFound)
						: string.Equals(previous, status, StringComparison.Ordinal)
							? new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.AlreadySet)
							: new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.Updated))
					.ToArray();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}

[JsonConverter(typeof(JsonStringEnumConverter<ScenarioStatusOutcome>))]
public enum ScenarioStatusOutcome
{
	Updated,
	AlreadySet,
	NotFound
}

/// What happened to one scenario. PreviousStatus is null when the scenario has no [Xml].[Response] row.
public sealed record ScenarioStatusChange(
	string ScenarioId,
	string? PreviousStatus,
	string NewStatus,
	ScenarioStatusOutcome Outcome);
```

### `PricingValidationFramework.DataLoader\SetStatusCommand.cs`

```csharp
namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;

/// `DataLoader set-status`: sets [Xml].[Response].Status to PASS or FAIL for a list of scenario IDs, e.g.
/// after a FAIL has been reviewed. Run by the set-status pipeline, or locally. Only the status and audit
/// columns change; see ScenarioStatusUpdater.
public static class SetStatusCommand
{
    public const int MaxScenarioIds = 5000;
    public static readonly string DefaultReportPath = Path.Combine("TestResults", "DataLoader", "set-status-summary.json");

    private static readonly string[] Options = ["--status", "--scenarios", "--scenarios-file", "--settings", "--report", "--markdown"];

    public static Task<int> RunAsync(string[] optionArgs, CancellationToken cancellationToken) =>
        RunAsync(optionArgs, (database, retry) => new ScenarioStatusUpdater(new SqlConnectionFactory(database), retry), cancellationToken);

    internal static async Task<int> RunAsync(
        string[] optionArgs,
        Func<DatabaseSettings, RetrySettings, IScenarioStatusUpdater> createUpdater,
        CancellationToken cancellationToken)
    {
        var reportPath = DefaultReportPath;
        string? markdownPath = null;
        var summary = new SetStatusSummary();
        try
        {
            var values = ParseOptions(optionArgs);
            reportPath = values.GetValueOrDefault("--report", reportPath);
            markdownPath = values.GetValueOrDefault("--markdown");

            summary.Status = values.TryGetValue("--status", out var status) ? status.Trim().ToUpperInvariant() : string.Empty;
            if (!ScenarioStatusUpdater.AllowedStatuses.Contains(summary.Status))
            {
                throw new ArgumentException("--status must be PASS or FAIL.");
            }

            var idText = values.GetValueOrDefault("--scenarios", string.Empty);
            if (values.TryGetValue("--scenarios-file", out var idFile))
            {
                // A .csv is read as a CSV (scenario_id column); any other file as IDs separated by commas, semicolons or lines.
                idText += "\n" + (Path.GetExtension(idFile).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    ? string.Join('\n', await ReadCsvScenarioIdsAsync(idFile, cancellationToken))
                    : await File.ReadAllTextAsync(idFile, cancellationToken));
            }

            var scenarioIds = ParseScenarioIds(idText);
            summary.Requested = scenarioIds.Count;

            var configuration = SeedCommand.BuildConfiguration(values.GetValueOrDefault("--settings"));
            var database = configuration.GetSection("DatabaseSettings").Get<DatabaseSettings>() ?? new DatabaseSettings();
            if (string.IsNullOrWhiteSpace(database.ConnectionString))
            {
                throw new InvalidOperationException("DatabaseSettings:ConnectionString is required (DatabaseSettings__ConnectionString or the --settings file).");
            }

            summary.ChangedBy = string.IsNullOrWhiteSpace(database.AuditUser) ? "(database login)" : database.AuditUser.Trim();
            var retry = configuration.GetSection("RetrySettings").Get<RetrySettings>() ?? new RetrySettings();

            Console.WriteLine($"set-status: setting {scenarioIds.Count} scenario(s) to {summary.Status}, recorded as {summary.ChangedBy}.");
            summary.Results.AddRange(await createUpdater(database, retry).SetStatusAsync(scenarioIds, summary.Status, cancellationToken));
            foreach (var change in summary.Results)
            {
                Console.WriteLine($"  {Describe(change.Outcome),-16} {change.ScenarioId}  {change.PreviousStatus ?? "-"} -> {change.NewStatus}");
            }
        }
        catch (Exception exception)
        {
            summary.Cancelled = exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
            summary.Issues.Add($"The status could not be set ({exception.GetType().Name}): {exception.Message}");
            Console.Error.WriteLine(summary.Issues[^1]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None);
        if (markdownPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(markdownPath))!);
            await File.WriteAllTextAsync(markdownPath, ToMarkdown(summary), CancellationToken.None);
        }

        Console.WriteLine($"set-status: updated={summary.Updated}, alreadySet={summary.AlreadySet}, notFound={summary.NotFound}, success={summary.Succeeded}, report={Path.GetFullPath(reportPath)}.");
        return summary.Cancelled ? 130 : summary.Succeeded ? 0 : 1;
    }

    /// IDs separated by commas, semicolons or new lines; trimmed, blanks dropped, duplicates removed
    /// (ignoring case, as SQL Server compares them). Order is kept.
    internal static IReadOnlyList<string> ParseScenarioIds(string text)
    {
        var ids = (text ?? string.Empty)
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ids.Length == 0)
        {
            throw new ArgumentException("No scenario IDs were given. Use --scenarios \"id1,id2\" or --scenarios-file <path>.");
        }

        if (ids.Length > MaxScenarioIds)
        {
            throw new ArgumentException($"{ids.Length} scenario IDs were given; the limit is {MaxScenarioIds} per run.");
        }

        var tooLong = ids.FirstOrDefault(id => id.Length > 200);
        return tooLong is null
            ? ids
            : throw new ArgumentException($"'{tooLong[..40]}...' is too long to be a scenario ID. Check the IDs are separated by commas.");
    }

    /// The scenario IDs from a CSV: the scenario_id column when the first row has that header (any case),
    /// otherwise the first column with no header row. Other columns are ignored, quoted values are
    /// unquoted, and blank rows are skipped. Every ID gets the run's one --status.
    internal static async Task<IReadOnlyList<string>> ReadCsvScenarioIdsAsync(string path, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvParser(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { DetectDelimiter = false });
        var ids = new List<string>();
        var column = 0;
        var firstRow = true;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = csv.Record ?? [];
            if (firstRow)
            {
                firstRow = false;
                var header = Array.FindIndex(row, cell => string.Equals(cell.Trim(), "scenario_id", StringComparison.OrdinalIgnoreCase));
                if (header >= 0)
                {
                    column = header;
                    continue;
                }
            }

            if (column < row.Length && !string.IsNullOrWhiteSpace(row[column]))
            {
                ids.Add(row[column].Trim());
            }
        }

        return ids;
    }

    /// The run summary as a Markdown table, for the pipeline's Summary tab.
    internal static string ToMarkdown(SetStatusSummary summary)
    {
        var text = new StringBuilder();
        if (summary.Results.Count == 0)
        {
            // Stopped before reaching the database (bad input or settings): nothing was changed.
            text.AppendLine("## Baseline status not changed");
        }
        else
        {
            text.AppendLine($"## Baseline status set to {summary.Status}");
            text.AppendLine();
            text.AppendLine($"Changed by **{summary.ChangedBy}**. {summary.Requested} requested: **{summary.Updated} updated**, {summary.AlreadySet} already {summary.Status}, **{summary.NotFound} not found**.");
        }

        foreach (var issue in summary.Issues)
        {
            text.AppendLine();
            text.AppendLine($"> **Error:** {issue}");
        }

        if (summary.Results.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("| Scenario ID | Before | After | Result |");
            text.AppendLine("|---|---|---|---|");
            foreach (var change in summary.Results)
            {
                text.AppendLine($"| {change.ScenarioId.Replace("|", "\\|")} | {change.PreviousStatus ?? "-"} | {(change.Outcome == ScenarioStatusOutcome.NotFound ? "-" : change.NewStatus)} | {Describe(change.Outcome)} |");
            }
        }

        return text.ToString();
    }

    private static string Describe(ScenarioStatusOutcome outcome) => outcome switch
    {
        ScenarioStatusOutcome.Updated => "Updated",
        ScenarioStatusOutcome.AlreadySet => "Already set",
        _ => "Not found"
    };

    private static Dictionary<string, string> ParseOptions(string[] optionArgs)
    {
        if (optionArgs.Length % 2 != 0)
        {
            throw new ArgumentException("Options must be --name value pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < optionArgs.Length; index += 2)
        {
            if (!Options.Contains(optionArgs[index]) || !values.TryAdd(optionArgs[index], optionArgs[index + 1]))
            {
                throw new ArgumentException($"Unknown or duplicate set-status option '{optionArgs[index]}'. It takes {string.Join(", ", Options)}.");
            }
        }

        return values;
    }
}

public sealed class SetStatusSummary
{
    public string Mode => "set-status";
    public string Status { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public int Requested { get; set; }
    public int Updated => Results.Count(change => change.Outcome == ScenarioStatusOutcome.Updated);
    public int AlreadySet => Results.Count(change => change.Outcome == ScenarioStatusOutcome.AlreadySet);
    public int NotFound => Results.Count(change => change.Outcome == ScenarioStatusOutcome.NotFound);
    public bool Cancelled { get; set; }

    /// False when anything was not found, so the person running it notices; the IDs that were found are still updated.
    public bool Succeeded => !Cancelled && Issues.Count == 0 && NotFound == 0;
    public List<string> Issues { get; } = [];
    public List<ScenarioStatusChange> Results { get; } = [];
}
```

### `azure-pipelines-set-status.yml`

```yaml
# Sets the baseline status ([Xml].[Response].Status) to PASS or FAIL for a list of scenario IDs, e.g. after
# someone has reviewed a Radar FAIL. Manual only: open the pipeline, click Run pipeline, fill in the form.
# The IDs come from a CSV committed to this repository; every ID in it is set to the one status chosen
# in the form.
# Only Status and the row_modifieddate/row_modifiedby audit columns change; the baseline XML, Last_updated
# and every other row are untouched. The person who starts the run is recorded in row_modifiedby.
# The Build stage tests and publishes the data loader once; the SetStatus stage runs that build.
trigger: none
pr: none

parameters:
  # No default, so the run form requires it.
  - name: scenarioIdsFile
    displayName: CSV of scenario IDs in this repository, e.g. PricingValidationFramework/data/status/ids.csv
    type: string
  - name: status
    displayName: New status
    type: string
    default: PASS
    values:
      - PASS
      - FAIL
variables:
  # Must be able to reach the database. Needed when the run is compiled, so it is set here.
  agentPool: Default

pool:
  name: ${{ variables.agentPool }}

stages:
  - stage: Build
    displayName: Build and test the data loader
    jobs:
      - job: BuildDataLoader
        timeoutInMinutes: 20
        workspace:
          clean: all
        steps:
          - checkout: self
          - task: UseDotNet@2
            displayName: Use .NET 10 SDK
            inputs:
              packageType: sdk
              version: 10.0.x
          - pwsh: |
              dotnet test PricingValidationFramework/PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj --configuration Release --filter "FullyQualifiedName~SetStatusCommandTests"
              if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
              dotnet publish PricingValidationFramework/PricingValidationFramework.DataLoader/PricingValidationFramework.DataLoader.csproj --configuration Release --output '$(Build.ArtifactStagingDirectory)/dataloader'
              exit $LASTEXITCODE
            displayName: Test set-status and publish data loader
          - task: PublishPipelineArtifact@1
            displayName: Publish dataloader artifact
            inputs:
              targetPath: '$(Build.ArtifactStagingDirectory)/dataloader'
              artifact: dataloader

  - stage: SetStatus
    displayName: Set baseline status
    dependsOn: Build
    jobs:
      - job: SetStatus
        displayName: Set baseline status
        timeoutInMinutes: 20
        workspace:
          clean: all
        steps:
          # Checked out for the scenario ID CSV; the loader comes from the Build stage.
          - checkout: self
          - download: current
            artifact: dataloader
          - task: UseDotNet@2
            displayName: Use .NET 10 runtime
            inputs:
              packageType: runtime
              version: 10.0.x

          - pwsh: |
              $output = '$(Build.ArtifactStagingDirectory)/SetStatus'
              New-Item -ItemType Directory -Force -Path $output | Out-Null
              # The file path comes in through an environment variable, never pasted into the script, so its content can't run as code.
              if ([string]::IsNullOrWhiteSpace($env:SCENARIO_IDS_FILE)) { Write-Host "##vso[task.logissue type=error]Enter the path of a scenario ID CSV in this repository."; exit 1 }
              $idFile = Join-Path '$(Build.SourcesDirectory)' $env:SCENARIO_IDS_FILE.Trim()
              if (-not (Test-Path -LiteralPath $idFile)) { Write-Host "##vso[task.logissue type=error]Scenario ID file not found in the repository: $($env:SCENARIO_IDS_FILE.Trim())"; exit 1 }
              dotnet '$(Pipeline.Workspace)/dataloader/PricingValidationFramework.DataLoader.dll' set-status --status '${{ parameters.status }}' --scenarios-file $idFile --report "$output/set-status-summary.json" --markdown "$output/set-status-summary.md"
              $exitCode = $LASTEXITCODE
              # Show the before/after table on the run's Summary tab.
              if (Test-Path "$output/set-status-summary.md") { Write-Host "##vso[task.uploadsummary]$output/set-status-summary.md" }
              exit $exitCode
            displayName: Set status to ${{ parameters.status }}
            env:
              SCENARIO_IDS_FILE: ${{ parameters.scenarioIdsFile }}
              DatabaseSettings__ConnectionString: $(StatusUpdateDatabaseConnectionString)
              DatabaseSettings__AuditUser: $(Build.RequestedForEmail)

          - task: PublishPipelineArtifact@1
            condition: always()
            displayName: Publish status summary
            inputs:
              targetPath: '$(Build.ArtifactStagingDirectory)/SetStatus'
              artifact: SetStatusSummary
```

## Changed files

### `.vscode\launch.json`

```diff
@@ -8,6 +8,8 @@
   // To debug the Radar or ICE tests: Terminal > Run Task > "Radar tests: wait for debugger" (or
   // "ICE tests: ..."). It builds, then prints "Process Id: NNNN, Name: testhost" and waits. Start
   // "4. Attach to Radar/ICE test host" and pick that testhost process; breakpoints then hit.
+  //
+  // "Control panel" starts the local page (http://localhost:5080) that runs steps 1-6 with buttons.
   "version": "0.2.0",
   "configurations": [
     {
@@ -43,6 +45,22 @@
       "console": "integratedTerminal",
       "stopAtEntry": false
     },
+    {
+      "name": "Control panel (opens http://localhost:5080)",
+      "type": "coreclr",
+      "request": "launch",
+      "preLaunchTask": "build ControlPanel",
+      "program": "${workspaceFolder}/PricingValidationFramework.ControlPanel/bin/Debug/net10.0/PricingValidationFramework.ControlPanel.dll",
+      "cwd": "${workspaceFolder}",
+      "env": { "ASPNETCORE_ENVIRONMENT": "Development" },
+      "console": "integratedTerminal",
+      "stopAtEntry": false,
+      "serverReadyAction": {
+        "action": "openExternally",
+        "pattern": "Control panel: (http://localhost:\\d+)",
+        "uriFormat": "%s"
+      }
+    },
     {
       "name": "4. Attach to Radar/ICE test host",
       "type": "coreclr",
@@ -50,4 +68,4 @@
       "processId": "${command:pickProcess}"
     }
   ]
-}
+}
\ No newline at end of file
```

### `.vscode\tasks.json`

```diff
@@ -12,6 +12,17 @@
       "problemMatcher": "$msCompile",
       "group": "build"
     },
+    {
+      "label": "build ControlPanel",
+      "type": "process",
+      "command": "dotnet",
+      "args": [
+        "build",
+        "${workspaceFolder}/PricingValidationFramework.ControlPanel/PricingValidationFramework.ControlPanel.csproj"
+      ],
+      "problemMatcher": "$msCompile",
+      "group": "build"
+    },
     {
       "label": "Radar tests: run",
       "type": "process",
```

### `PricingValidationFramework.DataLoader\PricingValidationFramework.DataLoader.csproj`

```diff
@@ -5,6 +5,10 @@
     <ImplicitUsings>enable</ImplicitUsings>
     <Nullable>enable</Nullable>
   </PropertyGroup>
+  <ItemGroup>
+    <InternalsVisibleTo Include="PricingValidationFramework.Tests" />
+  </ItemGroup>
+
   <ItemGroup>
     <PackageReference Include="CsvHelper" Version="33.1.0" />
     <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0" />
```

### `PricingValidationFramework.DataLoader\Program.cs`

```diff
@@ -8,6 +8,7 @@ const string Usage = """
     Usage:
       DataLoader <validate|import> --requests <csv-or-folder> [--batch-size <1-1000>] [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>]
       DataLoader seed [--settings <json>] [--report <json>] [--request-time <now|yyyy-MM-ddZHH:mm:ss>] [--max-parallel <1-16>]
+      DataLoader set-status --status <PASS|FAIL> --scenarios <id,id,...> [--scenarios-file <path>] [--settings <json>] [--report <json>] [--markdown <md>]
     """;
 if (args.Length == 0 || args[0] is "--help" or "-h")
 {
@@ -27,6 +28,12 @@ if (args[0] == "seed")
     return await SeedCommand.RunAsync(args[1..], cancellation.Token);
 }
 
+if (args[0] == "set-status")
+{
+    // Sets [Xml].[Response].Status to PASS or FAIL for the given scenario IDs (status and audit columns only).
+    return await SetStatusCommand.RunAsync(args[1..], cancellation.Token);
+}
+
 var reportPath = Path.Combine("TestResults", "DataLoader", "import-summary.json");
 ImportSummary summary;
 try
```

### `PricingValidationFramework.DataLoader\README.md`

````diff
@@ -96,6 +96,23 @@ dotnet run --project PricingValidationFramework.DataLoader -- seed --settings da
 
 To debug in VS Code, open the `PricingValidationFramework` folder and choose **3. DataLoader: seed baselines into [Xml].[Response]** in Run and Debug. The `.vscode` folder also has **1. validate** and **2. import** configurations using the same settings file, so a full local run is 1, 2, 3 and then the Radar tests, which read the same `local-settings.json` (its `PipelineSettings` section holds Radar's thresholds and filters). To debug Radar or ICE, run the task **Radar tests: wait for debugger** (or **ICE tests: ...**), then start **4. Attach to Radar/ICE test host** and pick the `testhost` process. Breakpoints that are useful: `BaselineSeeder.RunAsync` (the scenarios found), `RadarPricingService.CreateBaselineAsync` (route and URL), and `ResultUpdater.TryInsertBaselineAsync` (the insert).
 
+## Set Status
+
+`set-status` sets `[Xml].[Response].Status` to `PASS` or `FAIL` for a list of scenario IDs. The set-status pipeline runs it; it can also be run locally.
+
+```text
+DataLoader set-status --status <PASS|FAIL> --scenarios <id,id,...> [--scenarios-file <path>] [--settings <json>] [--report <json>] [--markdown <md>]
+```
+
+- **IDs:** separated by commas, semicolons or new lines, in `--scenarios` and/or a `--scenarios-file`. Blanks and duplicates are ignored. At most 5,000 per run.
+- **CSV files:** a `--scenarios-file` ending in `.csv` is read as a CSV. With a `scenario_id` header (any case, any column) that column is used and the others are ignored; without one, the first column is used and every row is an ID. Quoted values and Excel's byte-order mark are handled, and blank rows are skipped. Every ID gets the one `--status`; a CSV can't set different statuses per row.
+- **Status:** `PASS` or `FAIL` (any case). Anything else is rejected before the database is touched.
+- **Settings:** `DatabaseSettings.ConnectionString`, and optionally `DatabaseSettings.AuditUser` (the name recorded in `row_modifiedby`; otherwise the SQL login), from `--settings` and/or environment variables, as for import and seed.
+- **Output:** each ID with its previous and new status and whether it was `Updated`, `AlreadySet` or `NotFound`, on the console, in the JSON report (default `TestResults/DataLoader/set-status-summary.json`) and, with `--markdown`, as a table the pipeline shows on its Summary tab.
+- **Exit codes:** 0 when every ID was found; 1 when any wasn't found (the others are still updated) or the command couldn't run; 130 when cancelled.
+
+The update (Core `ScenarioStatusUpdater`) changes `Status`, `row_modifieddate` and `row_modifiedby` only, never `XML_Response` or `Last_updated`, and never inserts or deletes rows. Rows already at the requested status are left untouched, so their audit columns keep the last real change. IDs are processed 1,000 at a time, each batch in one transaction. It needs SELECT and UPDATE on `[Xml].[Response]`.
+
 ## Database Behavior
 
 The target schema must already exist. Enforce a unique/primary key on `[Xml].[Request].Scenario_id`. `XML_request` is `VARCHAR(MAX)` and its parameter is explicitly bound as ANSI MAX-length text. Ensure the database code page or UTF-8 collation supports the XML's characters. The importer does not create tables or change database constraints. Table names use the connection user's existing schema resolution, like the Core readers.
````

### `PricingValidationFramework.slnx`

```diff
@@ -1,4 +1,5 @@
 <Solution>
+  <Project Path="PricingValidationFramework.ControlPanel/PricingValidationFramework.ControlPanel.csproj" />
   <Project Path="PricingValidationFramework.Core/PricingValidationFramework.Core.csproj" />
   <Project Path="PricingValidationFramework.DataLoader/PricingValidationFramework.DataLoader.csproj" />
   <Project Path="PricingValidationFramework.Tests/PricingValidationFramework.Tests.csproj" />
```

### `README.md`

````diff
@@ -1,5 +1,25 @@
 # Pricing Validation Framework
 
+## Control panel
+
+`PricingValidationFramework.ControlPanel` is a local web page that runs each step with a button, on this machine, so nobody has to type commands:
+
+| Step | What it runs |
+|---|---|
+| 1. Choose scenarios CSV | Copies the chosen file into `data/import` as the newest `scenarios-{timestamp}.csv` and checks it offline (`DataLoader validate`) |
+| 2. Import | `DataLoader import` of that CSV into `[Xml].[Request]` |
+| 3. Seed baselines | `DataLoader seed` into `[Xml].[Response]` |
+| 4. Radar compare | The Radar tests, with the threshold type, min/max, product, tag and request time from the form |
+| 5. ICE check | The ICE tests: the newest PASS baseline per product/scheme, or exactly the scenarios that passed in the latest Radar run |
+| 6. Set status | `DataLoader set-status` to PASS or FAIL, for typed IDs and/or a file of IDs |
+
+To start it, open the `PricingValidationFramework` folder in VS Code and run **Control panel (opens http://localhost:5080)** in Run and Debug, or run `dotnet run --project PricingValidationFramework.ControlPanel` and open http://localhost:5080. Every step uses `data/import/local-settings.json` (copy `data/import/examples/local-settings.example.json`), as the VS Code launch configurations do; a box left empty on the page falls back to that file. The results are the same as running the commands yourself: Radar PASS updates the baseline, and FAIL sets the status.
+
+- One step runs at a time; the page shows its live output, a short result, and links to its summary and report files (`TestResults/ControlPanel` for DataLoader summaries; the Radar and ICE reports are named after the run, e.g. `Radar_local-20261021-093000-000.csv`).
+- It listens on `localhost` only and accepts changes only from its own page, so other websites open in the same browser can't trigger a step.
+- Settings are in `PricingValidationFramework.ControlPanel/appsettings.json`: the port, the settings file, and the build configuration.
+- The steps go through `IStepRunner`. `LocalStepRunner` runs them on this machine; a pipeline runner can implement the same interface later, queuing the Azure DevOps pipelines with the same inputs, without changing the page.
+
 ## Run the test suite
 
 ```bash
@@ -108,6 +128,25 @@ The chosen agent must reach the database and external endpoints. Replace the tem
 
 The Radar job timeout is 360 minutes. At the configured overall limit of two request starts per second, 20,000 API calls across any combination of endpoints require approximately 2 hours 47 minutes, before latency and retries. NUnit still has four scenario workers; the remaining scenarios wait to be scheduled rather than entering the limiter together. Ensure the agent entitlement permits a job of this duration; a free hosted job capped at 60 minutes cannot complete that workload even with the YAML timeout increased. Longer runs may require a self-hosted agent and a larger timeout. Local regression timings exclude SQL and live API calls and are not an end-to-end throughput guarantee.
 
+## Set a baseline's status
+
+Once someone has reviewed a Radar FAIL, they can set the scenario's baseline status back to PASS (or mark one FAIL) without touching the database themselves. Register `PricingValidationFramework/azure-pipelines-set-status.yml` as a manual pipeline, then:
+
+1. Open the pipeline and click **Run pipeline**.
+2. Enter the scenario IDs, separated by commas, e.g. `Carrot_CarrotTD_54798554, Carrot_CarrotTD_99185883`. For a longer list, commit a CSV of IDs to the repository and give its path instead, e.g. `PricingValidationFramework/data/status/ids.csv`. The CSV can have a `scenario_id` header (other columns are ignored, so a loader CSV works too) or just one ID per row. You can use both; the lists are merged.
+3. Choose the new status, **PASS** or **FAIL**, and click **Run**. Every ID in the run, from the box and the CSV, is set to that one status.
+
+The run's **Summary** tab shows each ID with its status before and after, and whether it was updated, already at that status, or not found. The same summary is published as the `SetStatusSummary` artifact. The run fails if any ID wasn't found, so typos are noticed; the IDs that were found are still updated. To undo a change, run it again with the other status.
+
+Only `Status`, `row_modifieddate` and `row_modifiedby` change. The baseline XML and `Last_updated` are never changed, and no rows are added or removed. The person who started the run (their Azure DevOps e-mail address) is recorded in `row_modifiedby`. Setting a FAIL back to PASS keeps the old baseline XML, so the next Radar run compares against it again.
+
+Set-up:
+- Add a secret pipeline variable `StatusUpdateDatabaseConnectionString`, using a login that doesn't need MFA and has SELECT and UPDATE on `[Xml].[Response]`. Nothing is created in the database.
+- The agent pool (the `agentPool` parameter, default `Default`) must be able to reach the database.
+- Give "Queue builds" permission on this pipeline only to the people who should change statuses. To require a second person to approve each run, add an approval check (an Azure DevOps environment) in front of it.
+
+The same change can be made locally: `dotnet run --project PricingValidationFramework.DataLoader -- set-status --status PASS --scenarios "id1,id2" --settings data/import/local-settings.json`. See the DataLoader README.
+
 ## Load scenario data
 
 `PricingValidationFramework.DataLoader` imports a `scenario_id,xml` requests CSV into `[Xml].[Request]` through the Core database repository, reading `Quote_ref`, `Product_code` and `Scheme_code` from `/Message/Policy`. Loader classes own CSV/XML validation and import policy; the repository owns SQL and atomic, parameterised multi-row inserts and updates using the existing connection factory, needing SELECT, INSERT and UPDATE on `[Xml].[Request]`. New IDs are inserted. An existing ID is compared on its XML only: identical (normalized) XML is skipped and any other change updates `XML_request` alone; quote reference, product, scheme, tags and creation date are not compared or changed. The default batch size is 100 and SQL statements are limited to 333 rows. `validate` and `import` don't call Radar or write `[Xml].[Response]`. A separate `seed` command creates the first baseline for every scenario that has no `[Xml].[Response]` row: it calls Radar at the request time (`--request-time`, then `RADAR_REQUEST_DATETIME`, default `now`) and stores each response as a `PASS` baseline without comparing it. Scenarios that already have a baseline, of any status, are never touched. It reads the Radar endpoints and database from a `--settings` JSON and/or the same environment variables as the Radar pipeline, and routes with `config/radar/mappings.json` (routing only, no XPath files). The data-loader pipeline runs it as its **Seed baselines** stage after the import, at the pipeline's `requestDateTime`. See [the DataLoader README](PricingValidationFramework.DataLoader/README.md) for running it locally.
````

