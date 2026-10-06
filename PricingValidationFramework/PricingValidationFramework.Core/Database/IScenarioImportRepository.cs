namespace PricingValidationFramework.Core.Database;

using PricingValidationFramework.Core.Models.Database;

public interface IScenarioImportRepository
{
	Task<ScenarioImportSnapshot> ReadExistingAsync(
		IReadOnlyCollection<string> scenarioIds,
		CancellationToken cancellationToken = default);

	Task InsertBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> requests,
		IReadOnlyCollection<ScenarioResponseImport> responses,
		CancellationToken cancellationToken = default);
}