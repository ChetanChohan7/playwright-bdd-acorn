using System.Globalization;
using System.Text.Json;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.DataLoader;

const string Usage = "Usage: DataLoader <validate|import> --requests <csv-or-folder> [--batch-size <1-1000>] [--settings <json>] [--report <json>]";
if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 2 : 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};
var reportPath = Path.Combine("TestResults", "DataLoader", "import-summary.json");
ImportSummary summary;
try
{
    if (args[0] is not ("validate" or "import") || (args.Length - 1) % 2 != 0)
    {
        throw new ArgumentException("A valid command and option/value pairs are required.");
    }
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < args.Length; index += 2)
    {
        if (args[index] is not ("--requests" or "--batch-size" or "--settings" or "--report") ||
            !values.TryAdd(args[index], args[index + 1]))
        {
            throw new ArgumentException("An unknown or duplicate command option was supplied.");
        }
    }
    reportPath = values.GetValueOrDefault("--report", reportPath);
    var batchSize = int.Parse(values.GetValueOrDefault("--batch-size", "500"), CultureInfo.InvariantCulture);
    IScenarioImportRepository? repository = null;
    if (args[0] == "import")
    {
        var connectionString = Environment.GetEnvironmentVariable("DatabaseSettings__ConnectionString");
        if (connectionString is null && values.TryGetValue("--settings", out var settingsPath))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath, cancellation.Token));
            connectionString = settings.RootElement.GetProperty("DatabaseSettings").GetProperty("ConnectionString").GetString();
        }
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Import requires a database connection string.");
        }
        repository = new ScenarioImportRepository(new SqlConnectionFactory(new DatabaseSettings { ConnectionString = connectionString }));
    }
    summary = await new ScenarioDataLoader(repository).RunAsync(new ImportOptions(
        values.GetValueOrDefault("--requests") ?? throw new ArgumentException("--requests is required."),
        args[0] == "validate", batchSize), cancellation.Token);
}
catch (Exception exception)
{
    summary = new ImportSummary { Mode = args[0], Cancelled = exception is OperationCanceledException };
    summary.Issues.Add(new ImportIssue("configuration", 0, string.Empty,
        $"The command could not start ({exception.GetType().Name}). Check arguments, file paths, and database configuration."));
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{summary.Mode}: file={summary.RequestFile}, success={summary.Succeeded}, requests={summary.InputRequests}, " +
    $"insertedRequests={summary.InsertedRequests}, skippedRequests={summary.SkippedRequests}, issues={summary.Issues.Count}.");
return summary.Cancelled ? 130 : summary.Succeeded ? 0 : 1;