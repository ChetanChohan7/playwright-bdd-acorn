namespace PricingValidationFramework.DataLoader;

using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.External;

public sealed record ImportOptions(
    string? RequestCsv,
    string? ResponseCsv,
    bool ValidateOnly,
    int BatchSize = 500,
    string BuildId = "local-import");

public sealed record ImportIssue(string Source, int RecordNumber, string ScenarioId, string Reason);

public sealed class ImportSummary
{
    public string Mode { get; init; } = string.Empty;
    public int InputRequests { get; set; }
    public int InputResponses { get; set; }
    public int InsertedRequests { get; set; }
    public int InsertedResponses { get; set; }
    public int SkippedRequests { get; set; }
    public int SkippedResponses { get; set; }
    public int CommittedBatches { get; set; }
    public bool Cancelled { get; set; }
    public List<ImportIssue> Issues { get; } = [];
    public bool Succeeded => !Cancelled && Issues.Count == 0;
}

public sealed class ScenarioDataLoader(IScenarioImportRepository? repository = null)
{
    private static readonly JsonSerializerOptions EnvelopeOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<ImportSummary> RunAsync(ImportOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BatchSize is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Batch size must be between 1 and 1000.");
        }
        if (string.IsNullOrWhiteSpace(options.RequestCsv) && string.IsNullOrWhiteSpace(options.ResponseCsv))
        {
            throw new ArgumentException("At least one request or response CSV is required.", nameof(options));
        }

        var summary = new ImportSummary { Mode = options.ValidateOnly ? "validate" : "import" };
        using var spool = new ImportSpool();
        var operation = "Reading input files";
        try
        {
            await LoadRequestsAsync(options, spool, summary, cancellationToken);
            await LoadResponsesAsync(options, spool, summary, cancellationToken);
            if (summary.InputRequests + summary.InputResponses == 0)
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
            var responseInserts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            operation = "Checking existing database records";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var existing = await repository.ReadExistingAsync(batch.Select(row => row.ScenarioId).ToArray(), cancellationToken);
                PlanBatch(batch, existing, spool, summary, requestInserts, responseInserts);
            }
            if (!summary.Succeeded)
            {
                return summary;
            }

            operation = "Inserting a database batch";
            foreach (var batch in spool.ReadBatches(options.BatchSize, cancellationToken))
            {
                var requests = batch.Where(row => requestInserts.Contains(row.ScenarioId)).Select(row => row.Request!).ToArray();
                var responses = batch.Where(row => responseInserts.Contains(row.ScenarioId)).Select(row => row.Response!).ToArray();
                if (requests.Length == 0 && responses.Length == 0)
                {
                    continue;
                }
                await repository.InsertBatchAsync(requests, responses, cancellationToken);
                summary.InsertedRequests += requests.Length;
                summary.InsertedResponses += responses.Length;
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

    private static async Task LoadRequestsAsync(ImportOptions options, ImportSpool spool, ImportSummary summary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.RequestCsv))
        {
            return;
        }
        await foreach (var row in CsvScenarioReader.ReadRequestsAsync(options.RequestCsv, cancellationToken))
        {
            summary.InputRequests++;
            try
            {
                var normalized = NormalizeRequest(row);
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

    private static async Task LoadResponsesAsync(ImportOptions options, ImportSpool spool, ImportSummary summary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ResponseCsv))
        {
            return;
        }
        await foreach (var row in CsvScenarioReader.ReadResponsesAsync(options.ResponseCsv, cancellationToken))
        {
            summary.InputResponses++;
            try
            {
                var normalized = NormalizeResponse(row, options.BuildId);
                if (!spool.AddResponse(normalized))
                {
                    throw new InvalidDataException("Duplicate response Scenario_id.");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or XmlException or JsonException)
            {
                summary.Issues.Add(new ImportIssue("responses", summary.InputResponses, row.ScenarioId,
                    exception is InvalidDataException ? exception.Message : "Response XML or JSON envelope is malformed."));
            }
        }
    }

    private static void PlanBatch(
        IReadOnlyList<ScenarioImportEntry> batch,
        ScenarioImportSnapshot existing,
        ImportSpool spool,
        ImportSummary summary,
        HashSet<string> requestInserts,
        HashSet<string> responseInserts)
    {
        var requests = existing.Requests.ToDictionary(row => row.ScenarioId, StringComparer.OrdinalIgnoreCase);
        var responses = existing.Responses.ToDictionary(row => row.ScenarioId, StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch)
        {
            if (row.Request is not null)
            {
                if (!requests.TryGetValue(row.ScenarioId, out var storedRequest))
                {
                    requestInserts.Add(row.ScenarioId);
                }
                else if (NormalizeRequest(storedRequest) == row.Request)
                {
                    summary.SkippedRequests++;
                }
                else
                {
                    summary.Issues.Add(new ImportIssue("database", 0, row.ScenarioId, "Request conflicts with an existing scenario; replacement is not allowed."));
                }
            }
            if (row.Response is null)
            {
                continue;
            }
            if (!spool.ContainsRequest(row.ScenarioId) && !requests.ContainsKey(row.ScenarioId))
            {
                summary.Issues.Add(new ImportIssue("database", 0, row.ScenarioId, "Response has no matching request Scenario_id."));
                continue;
            }
            if (!responses.TryGetValue(row.ScenarioId, out var storedResponse))
            {
                responseInserts.Add(row.ScenarioId);
            }
            else
            {
                var normalized = NormalizeResponse(storedResponse, string.Empty);
                if (normalized.XmlResponse == row.Response.XmlResponse && normalized.Status == row.Response.Status)
                {
                    summary.SkippedResponses++;
                }
                else
                {
                    summary.Issues.Add(new ImportIssue("database", 0, row.ScenarioId, "Response conflicts with an existing baseline; replacement is not allowed."));
                }
            }
        }
    }

    private static ScenarioRequestImport NormalizeRequest(ScenarioRequestImport row)
    {
        return new ScenarioRequestImport(Required(row.ScenarioId, "Scenario_id"), Required(row.QuoteRef, "Quote_ref"),
            Required(row.ProductCode, "Product_code"), Required(row.SchemeCode, "Schem_code"),
            NormalizeXml(row.XmlRequest, false), row.TestTags?.Trim() ?? string.Empty);
    }

    private static ScenarioResponseImport NormalizeResponse(ScenarioResponseImport row, string defaultBuildId)
    {
        var status = Required(row.Status, "Status").ToUpperInvariant();
        if (status is not ("PASS" or "FAIL" or "ERROR"))
        {
            throw new InvalidDataException("Status must be explicitly set to PASS, FAIL, or ERROR.");
        }
        return new ScenarioResponseImport(Required(row.ScenarioId, "Scenario_id"), NormalizeXml(row.XmlResponse, true),
            Required(string.IsNullOrWhiteSpace(row.BuildId) ? defaultBuildId : row.BuildId, "Build_id"), status);
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{field} is required.")
            : value.Trim();
    }

    private static string NormalizeXml(string value, bool allowEnvelope)
    {
        var xml = Required(value, "XML").TrimStart('\uFEFF').Trim();
        if (allowEnvelope && !xml.StartsWith('<'))
        {
            var envelope = JsonSerializer.Deserialize<RadarJsonResponse>(xml, EnvelopeOptions);
            xml = Required(envelope?.Response, "JSON response XML").TrimStart('\uFEFF').Trim();
        }
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader).Root?.ToString(SaveOptions.DisableFormatting)
            ?? throw new InvalidDataException("XML has no root element.");
    }
}