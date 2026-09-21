using FuzzyPricingMatcher.Tests.Processing;

namespace FuzzyPricingMatcher.Tests.Loader;

public sealed record BaselineDatabaseSnapshot(IReadOnlyList<ExistingRequestRow> RequestRows, IReadOnlyList<ExistingResponseRow> ResponseRows);

public sealed class BaselineScenarioCsvRow
{
    public int RowNumber { get; init; }
    public string ScenarioId { get; init; } = string.Empty;
    public string XmlRequest { get; init; } = string.Empty;
    public string TestTags { get; init; } = string.Empty;
}

public sealed record DeleteObsoleteScenarioCommand(IReadOnlyList<string> ScenarioIds);

public sealed record ExistingRequestRow(string ScenarioId, string QuoteRef, string XmlRequest, IReadOnlyList<string> NormalizedTags);

public sealed record ExistingResponseRow(string ScenarioId, string QuoteRef, string XmlResponse, string BuildId, string? Status);

public sealed record InsertBaselineScenarioCommand(string ScenarioId, string QuoteRef, string RawXml, IReadOnlyList<string> NormalizedTags, string RawResponseXml, string BuildId);

public sealed record LoaderApiRequest(string ScenarioId, string QuoteRef, string RawXml);

public sealed record LoaderApiResponse(string RawXml);

public sealed record LoaderScenarioResult
{
    public required string ScenarioId { get; init; }
    public string QuoteRef { get; init; } = string.Empty;
    public bool Successful { get; init; }
    public required LoaderScenarioOutcome Outcome { get; init; }
    public bool ApiCalled { get; init; }
    public string RequestTableAction { get; init; } = "None";
    public string ResponseTableAction { get; init; } = "None";
    public string Error { get; init; } = string.Empty;
}

public sealed record LoaderSynchronizationResult(IReadOnlyList<LoaderScenarioResult> ScenarioResults, string DeletionSkippedReason = "")
{
    public bool Successful => ScenarioResults.All(result => result.Successful) && string.IsNullOrWhiteSpace(DeletionSkippedReason);
    public bool HasErrors => ScenarioResults.Any(result => !result.Successful);
}

public sealed class PreparedBaselineScenario
{
    public required BaselineScenarioCsvRow CsvRow { get; init; }
    public required string NormalizedScenarioId { get; init; }
    public required RequestXmlMetadata RequestMetadata { get; init; }
    public required IReadOnlyList<string> NormalizedTags { get; init; }
    public required string XmlFingerprint { get; init; }
}

public sealed record RouteDefinition(string SchemeCode, string EndpointName, string ResponseSchemaFile = "", string ResponseProcessorName = "");

public sealed record UpdateBaselineScenarioCommand(string ScenarioId, string QuoteRef, string RawXml, IReadOnlyList<string> NormalizedTags, string RawResponseXml, string BuildId);
