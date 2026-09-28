using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader   // is there a need for this interfeace 
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    Task<ScenarioResponse> GetPassingBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}