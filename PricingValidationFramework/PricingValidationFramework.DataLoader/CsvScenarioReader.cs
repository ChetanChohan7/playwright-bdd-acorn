namespace PricingValidationFramework.DataLoader;

using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using PricingValidationFramework.Core.Models.Database;

internal static class CsvScenarioReader
{
    public static async IAsyncEnumerable<ScenarioRequestImport> ReadRequestsAsync(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in ReadAsync<RequestRow>(path, cancellationToken))
        {
            yield return new ScenarioRequestImport(row.ScenarioId, row.QuoteRef, row.ProductCode,
                row.SchemeCode, row.XmlRequest, row.TestTags);
        }
    }

    public static async IAsyncEnumerable<ScenarioResponseImport> ReadResponsesAsync(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in ReadAsync<ResponseRow>(path, cancellationToken))
        {
            yield return new ScenarioResponseImport(row.ScenarioId, row.XmlResponse, row.BuildId, row.Status);
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

    private sealed class RequestRow
    {
        [Name("Scenario_id")] public string ScenarioId { get; set; } = string.Empty;
        [Name("Quote_ref")] public string QuoteRef { get; set; } = string.Empty;
        [Name("Product_code")] public string ProductCode { get; set; } = string.Empty;
        [Name("Schem_code")] public string SchemeCode { get; set; } = string.Empty;
        [Name("XML_request")] public string XmlRequest { get; set; } = string.Empty;
        [Name("Test_tags")] public string TestTags { get; set; } = string.Empty;
    }

    private sealed class ResponseRow
    {
        [Name("Scenario_id")] public string ScenarioId { get; set; } = string.Empty;
        [Name("XML_Response")] public string XmlResponse { get; set; } = string.Empty;
        [Name("Build_id")] public string BuildId { get; set; } = string.Empty;
        [Name("Status")] public string Status { get; set; } = string.Empty;
    }
}