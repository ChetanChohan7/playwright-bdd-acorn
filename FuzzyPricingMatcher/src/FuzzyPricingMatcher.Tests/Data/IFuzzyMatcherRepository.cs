using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Data;

public interface IFuzzyMatcherRepository
{
    Task<IReadOnlyList<string>> GetAllRequestScenarioIdsAsync(CancellationToken cancellationToken = default);
    Task<DatabaseRequestRecord?> GetRequestAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<DatabaseResponseRecord?> GetResponseAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<DatabaseScenarioSnapshot> GetScenarioForComparisonAsync(string scenarioId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseDuplicateResult>> FindDuplicatesAsync(CancellationToken cancellationToken = default);
    Task InsertBaselineAsync(DatabaseInsertCommand command, CancellationToken cancellationToken = default);
    Task UpdateBaselineAsync(DatabaseUpdateCommand command, CancellationToken cancellationToken = default);
    Task UpdateTagsAsync(DatabaseTagsUpdateCommand command, CancellationToken cancellationToken = default);
    Task DeleteObsoleteAsync(IReadOnlyList<string> scenarioIds, CancellationToken cancellationToken = default);
    Task UpdateComparisonPassAsync(DatabaseComparisonPassCommand command, CancellationToken cancellationToken = default);
    Task UpdateComparisonFailAsync(DatabaseComparisonFailCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseScenarioSelection>> SelectScenariosAsync(string? requestedTags, TagMatchMode matchMode, CancellationToken cancellationToken = default);
}