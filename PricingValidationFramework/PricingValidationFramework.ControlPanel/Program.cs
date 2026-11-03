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
