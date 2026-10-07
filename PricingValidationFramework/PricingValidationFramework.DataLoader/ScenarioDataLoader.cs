namespace PricingValidationFramework.DataLoader;

using System.Xml;
using System.Xml.Linq;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;

public sealed record ImportOptions(
    string RequestCsv,
    bool ValidateOnly,
    int BatchSize = 500);

public sealed record ImportIssue(string Source, int RecordNumber, string ScenarioId, string Reason);

public sealed class ImportSummary
{
    public string Mode { get; init; } = string.Empty;
    public string RequestFile { get; set; } = string.Empty;
    public int InputRequests { get; set; }
    public int InsertedRequests { get; set; }
    public int SkippedRequests { get; set; }
    public int CommittedBatches { get; set; }
    public bool Cancelled { get; set; }
    public List<ImportIssue> Issues { get; } = [];
    public bool Succeeded => !Cancelled && Issues.Count == 0;
}

/// Loads xml_request only. Baselines in xml_response are created by the Radar run: a scenario
/// with no xml_response row gets Radar's first response stored as its PASS baseline.
public sealed class ScenarioDataLoader(IScenarioImportRepository? repository = null)
{
    public async Task<ImportSummary> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BatchSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Batch size must be between 1 and 1000.");
        }
        if (string.IsNullOrWhiteSpace(options.RequestCsv))
        {
            throw new ArgumentException("A request CSV is required.", nameof(options));
        }

        var summary = new ImportSummary { Mode = options.ValidateOnly ? "validate" : "import" };
        try
        {
            summary.RequestFile = RequestFileResolver.Resolve(options.RequestCsv);
        }
        catch (InvalidDataException exception)
        {
            summary.Issues.Add(new ImportIssue("input", 0, string.Empty, exception.Message));
            return summary;
        }

        using var spool = new ImportSpool();
        var operation = "Reading input files";
        try
        {
            await LoadRequestsAsync(summary.RequestFile, spool, summary, cancellationToken);
            if (summary.InputRequests == 0)
            {
                summary.Issues.Add(new ImportIssue("input", 0, string.Empty, "The input contains no scenario records."));
            }
            if (!summary.Succeeded || options.ValidateOnly)
            {
                return summary;
            }
            if (repository is null)
            {
                throw new InvalidOperationException("Import requires a database repository.");
            }

            var requestInserts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            operation = "Checking existing database records";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var existing = await repository.ReadExistingAsync(batch.Select(row => row.ScenarioId).ToArray(), cancellationToken);
                PlanBatch(batch, existing, summary, requestInserts);
            }
            if (!summary.Succeeded)
            {
                return summary;
            }

            operation = "Inserting a database batch";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var requests = batch.Where(row => requestInserts.Contains(row.ScenarioId)).ToArray();
                if (requests.Length == 0)
                {
                    continue;
                }
                await repository.InsertBatchAsync(requests, cancellationToken);
                summary.InsertedRequests += requests.Length;
                summary.CommittedBatches++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            summary.Cancelled = true;
        }
        catch (Exception exception)
        {
            summary.Issues.Add(new ImportIssue("operation", 0, string.Empty,
                $"{operation} failed ({exception.GetType().Name}). Check database state before retrying; earlier batches may already be committed."));
        }
        return summary;
    }

    private static async Task LoadRequestsAsync(string requestFile, ImportSpool spool, ImportSummary summary, CancellationToken cancellationToken)
    {
        await foreach (var row in CsvScenarioReader.ReadRequestsAsync(requestFile, cancellationToken))
        {
            summary.InputRequests++;
            try
            {
                var normalized = RequestFromCsv(row);
                if (!spool.AddRequest(normalized))
                {
                    throw new InvalidDataException("Duplicate request Scenario_id.");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or XmlException)
            {
                summary.Issues.Add(new ImportIssue("requests", summary.InputRequests, row.ScenarioId,
                    exception is XmlException ? "Request XML is malformed or contains a prohibited DTD." : exception.Message));
            }
        }
    }

    private static void PlanBatch(
        IReadOnlyList<ScenarioRequestImport> batch,
        ScenarioImportSnapshot existing,
        ImportSummary summary,
        HashSet<string> requestInserts)
    {
        var requests = existing.Requests.ToDictionary(row => row.ScenarioId, StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch)
        {
            if (!requests.TryGetValue(row.ScenarioId, out var storedRequest))
            {
                requestInserts.Add(row.ScenarioId);
            }
            // Test_tags isn't in the CSV, so tags set on an existing row don't count as a conflict.
            else if (NormalizeRequest(storedRequest) with { TestTags = string.Empty } == row)
            {
                summary.SkippedRequests++;
            }
            else
            {
                summary.Issues.Add(new ImportIssue("database", 0, row.ScenarioId, "Request conflicts with an existing scenario; replacement is not allowed."));
            }
        }
    }

    private static ScenarioRequestImport RequestFromCsv(RequestCsvRow row)
    {
        var scenarioId = Required(row.ScenarioId, "scenario_id");
        var xml = NormalizeXml(row.Xml);
        var policy = XElement.Parse(xml) is { Name.LocalName: "Message" } message
            ? SingleChild(message, "Policy", "/Message/Policy")
            : throw new InvalidDataException("Request XML root element must be <Message>.");
        return new ScenarioRequestImport(scenarioId,
            Required(SingleChild(policy, "PolicyReference", "/Message/Policy/PolicyReference").Value, "PolicyReference (Quote_ref)"),
            Required(SingleChild(policy, "ProductCode", "/Message/Policy/ProductCode").Value, "ProductCode (Product_code)"),
            Required(SingleChild(policy, "SchemeCode", "/Message/Policy/SchemeCode").Value, "SchemeCode (Scheme_code)"),
            xml, string.Empty);
    }

    private static XElement SingleChild(XElement parent, string name, string path)
    {
        var matches = parent.Elements().Where(element => element.Name.LocalName == name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(matches.Length == 0
                ? $"Request XML has no {path} element."
                : $"Request XML has more than one {path} element.");
    }

    private static ScenarioRequestImport NormalizeRequest(ScenarioRequestImport row)
    {
        return new ScenarioRequestImport(Required(row.ScenarioId, "Scenario_id"), Required(row.QuoteRef, "Quote_ref"),
            Required(row.ProductCode, "Product_code"), Required(row.SchemeCode, "Scheme_code"),
            NormalizeXml(row.XmlRequest), row.TestTags?.Trim() ?? string.Empty);
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{field} is required.")
            : value.Trim();
    }

    private static string NormalizeXml(string value)
    {
        var xml = Required(value, "XML").TrimStart('﻿').Trim();
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader).Root?.ToString(SaveOptions.DisableFormatting)
            ?? throw new InvalidDataException("XML has no root element.");
    }
}
