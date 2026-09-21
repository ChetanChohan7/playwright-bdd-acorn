namespace FuzzyPricingMatcher.Tests.Database;

public sealed record DatabaseComparisonFailCommand(string ScenarioId, string BuildId);

public sealed record DatabaseComparisonPassCommand(string ScenarioId, string XmlResponse, string BuildId);

public sealed record DatabaseDuplicateResult(string TableName, string ScenarioId, int RowCount);

public sealed record DatabaseInsertCommand(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, string XmlResponse, string BuildId);

public sealed record DatabaseRequestRecord(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, DateTime CreatedDate);

public sealed record DatabaseResponseRecord(string ScenarioId, string QuoteRef, string XmlResponse, string BuildId, DateTime CreatedDate, DateTime? LastUpdated, string? Status);

public sealed record DatabaseScenarioSelection(string ScenarioId, string QuoteRef);

public sealed record DatabaseScenarioSnapshot(DatabaseRequestRecord? Request, DatabaseResponseRecord? Response);

public sealed record DatabaseTagsUpdateCommand(string ScenarioId, string TestTags);

public sealed record DatabaseUpdateCommand(string ScenarioId, string QuoteRef, string XmlRequest, string TestTags, string XmlResponse, string BuildId);
