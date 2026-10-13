using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Core.Database;

public interface IBaselineDataReader
{
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default);

    /// The same selection within Radar's filters: an exact Product_code (TEST_TAG) and/or one
    /// Test_tags tag (SCENARIO_TAG). A null or empty filter is not applied.
    Task<IReadOnlyList<IceBaselineScenario>> GetFilteredPassingBaselineScenariosAsync(
        string? productCode,
        string? scenarioTag,
        CancellationToken cancellationToken = default);

    /// The PASS baselines of exactly these scenarios, e.g. the ones that passed in a Radar run.
    Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselinesByScenarioIdsAsync(
        IReadOnlyCollection<string> scenarioIds,
        CancellationToken cancellationToken = default);

    /// The scenario's [Xml].[Response] row whatever its status, or null if it has no baseline yet.
    Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
        string scenarioId,
        CancellationToken cancellationToken = default);
}