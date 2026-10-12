namespace PricingValidationFramework.DataLoader;

using System.Xml;
using System.Xml.Linq;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;

public sealed record ImportOptions(
    string RequestCsv,
    bool ValidateOnly,
    int BatchSize = 100,
    string? RequestTime = null);

public sealed record ImportIssue(string Source, int RecordNumber, string ScenarioId, string Reason);

public sealed class ImportSummary
{
    public string Mode { get; init; } = string.Empty;
    public string RequestFile { get; set; } = string.Empty;

    /// The Radar request time for the baselines created after this load, resolved the same way
    /// Radar does (UTC, start of the hour). Empty when no --request-time was given.
    public string RequestTime { get; set; } = string.Empty;
    public int InputRequests { get; set; }
    public int InsertedRequests { get; set; }
    public int UpdatedRequests { get; set; }
    public int SkippedRequests { get; set; }
    public int CommittedBatches { get; set; }
    public bool Cancelled { get; set; }
    public List<ImportIssue> Issues { get; } = [];
    public bool Succeeded => !Cancelled && Issues.Count == 0;
}

/// Loads [Xml].[Request] only. Baselines in [Xml].[Response] are created by the Radar run: a scenario
/// with no [Xml].[Response] row gets Radar's first response stored as its PASS baseline.
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
        if (options.RequestTime is not null)
        {
            try
            {
                // "now" (any case) means the current UTC hour, exactly as an empty value does for Radar.
                summary.RequestTime = RequestTimeFormatter.Resolve(
                    string.Equals(options.RequestTime.Trim(), "now", StringComparison.OrdinalIgnoreCase) ? null : options.RequestTime);
            }
            catch (ArgumentException)
            {
                summary.Issues.Add(new ImportIssue("input", 0, string.Empty,
                    $"Request time '{options.RequestTime}' must be 'now' or use the yyyy-MM-ddZHH:mm:ss format, e.g. 2026-10-12Z09:00:00."));
                return summary;
            }
        }

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
            var requestUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            operation = "Checking existing database records";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var existing = await repository.ReadExistingAsync(batch.Select(row => row.ScenarioId).ToArray(), cancellationToken);
                PlanBatch(batch, existing, summary, requestInserts, requestUpdates);
            }
            if (!summary.Succeeded)
            {
                return summary;
            }

            operation = "Writing a database batch";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var inserts = batch.Where(row => requestInserts.Contains(row.ScenarioId)).ToArray();
                var updates = batch.Where(row => requestUpdates.Contains(row.ScenarioId)).ToArray();
                if (inserts.Length + updates.Length == 0)
                {
                    continue;
                }
                var result = await repository.ApplyBatchAsync(inserts, updates, cancellationToken);
                summary.InsertedRequests += result.InsertedRequests;
                summary.UpdatedRequests += result.UpdatedRequests;
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
        HashSet<string> requestInserts,
        HashSet<string> requestUpdates)
    {
        var requests = existing.Requests.ToDictionary(row => row.ScenarioId, StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch)
        {
            if (!requests.TryGetValue(row.ScenarioId, out var storedRequest))
            {
                requestInserts.Add(row.ScenarioId);
            }
            // An existing scenario is compared on its XML only: identical (after normalising) is
            // skipped, anything else updates XML_request. No other column is compared or changed.
            else if (string.Equals(TryNormalizeXml(storedRequest.XmlRequest), row.XmlRequest, StringComparison.Ordinal))
            {
                summary.SkippedRequests++;
            }
            else
            {
                requestUpdates.Add(row.ScenarioId);
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

    // Stored XML that can't be parsed simply counts as different, so a corrected CSV replaces it.
    private static string? TryNormalizeXml(string? value)
    {
        try
        {
            return NormalizeXml(value ?? string.Empty);
        }
        catch (Exception exception) when (exception is XmlException or InvalidDataException)
        {
            return null;
        }
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
