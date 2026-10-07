namespace PricingValidationFramework.Core.Database;

using PricingValidationFramework.Core.Models.Database;

public interface IScenarioImportRepository
{
	Task<ScenarioImportSnapshot> ReadExistingAsync(
		IReadOnlyCollection<string> scenarioIds,
		CancellationToken cancellationToken = default);

	Task<ScenarioImportBatchResult> ApplyBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> inserts,
		IReadOnlyCollection<ScenarioRequestImport> updates,
		CancellationToken cancellationToken = default);
}