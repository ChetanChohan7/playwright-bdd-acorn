namespace PricingValidationFramework.Core.Models.Database;

public sealed record ScenarioRequestImport(
	string ScenarioId,
	string QuoteRef,
	string ProductCode,
	string SchemeCode,
	string XmlRequest,
	string TestTags);

public sealed record ScenarioImportSnapshot(
	IReadOnlyList<ScenarioRequestImport> Requests);