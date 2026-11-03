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
