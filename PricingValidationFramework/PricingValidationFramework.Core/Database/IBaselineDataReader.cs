using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    /// The same selection limited to one Product_code (the TEST_TAG filter Radar uses).
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosByProductCodeAsync(
        string productCode,
        CancellationToken cancellationToken = default);

    /// The scenario's [Xml].[Response] row whatever its status, or null if it has no baseline yet.
    Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}