using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    /// The scenario's xml_response row whatever its status, or null if it has no baseline yet.
    Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}