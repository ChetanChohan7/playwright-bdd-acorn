using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    Task<ScenarioResponse> GetPassingBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}