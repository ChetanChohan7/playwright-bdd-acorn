namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

internal sealed record RequestCsvRow(string ScenarioId, string Xml);

internal static class CsvScenarioReader
{
    public static async IAsyncEnumerable<RequestCsvRow> ReadRequestsAsync(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in ReadAsync<RequestRow>(path, cancellationToken))
        {
            yield return new RequestCsvRow(row.ScenarioId, row.Xml);
        }
    }

    private static async IAsyncEnumerable<TRecord> ReadAsync<TRecord>(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            ExceptionMessagesContainRawData = false
        });
        await foreach (var row in csv.GetRecordsAsync<TRecord>(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    // Same shape as the scenarios.csv the CSV loader UI produces: scenario_id + request XML.
    // Quote_ref, Product_code and Scheme_code are read from the XML by ScenarioDataLoader.
    private sealed class RequestRow
    {
        [Name("scenario_id")] public string ScenarioId { get; set; } = string.Empty;
        [Name("xml")] public string Xml { get; set; } = string.Empty;
    }
}