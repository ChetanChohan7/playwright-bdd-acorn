namespace PricingValidationFramework.Core.Database;

using PricingValidationFramework.Core.Models.Database;

public interface IScenarioImportRepository
{
	Task<ScenarioImportSnapshot> ReadExistingAsync(
		IReadOnlyCollection<string> scenarioIds,
		CancellationToken cancellationToken = default);

	Task InsertBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> requests,
		CancellationToken cancellationToken = default);
}