using FuzzyPricingMatcher.Tests.Processing;

namespace FuzzyPricingMatcher.Tests.Loader;

public sealed class LoaderSynchronizationService
{
    private readonly ILoaderRepository repository;
    private readonly ILoaderRouteResolver routeResolver;
    private readonly ILoaderApiClient apiClient;
    private readonly ILoaderResponseValidator responseValidator;
    private readonly ILoaderEvidenceWriter evidenceWriter;
    private readonly RequestXmlMetadataReader metadataReader;
    private readonly XmlFingerprintService fingerprintService;
    private readonly IReadOnlyList<PreparedBaselineScenario> emptyRecords = Array.Empty<PreparedBaselineScenario>();

    public LoaderSynchronizationService(ILoaderRepository repository, ILoaderRouteResolver routeResolver, ILoaderApiClient apiClient, ILoaderResponseValidator responseValidator, ILoaderEvidenceWriter evidenceWriter, RequestXmlMetadataReader? metadataReader = null, XmlFingerprintService? fingerprintService = null)
    {
        this.repository = repository;
        this.routeResolver = routeResolver;
        this.apiClient = apiClient;
        this.responseValidator = responseValidator;
        this.evidenceWriter = evidenceWriter;
        this.metadataReader = metadataReader ?? new RequestXmlMetadataReader();
        this.fingerprintService = fingerprintService ?? new XmlFingerprintService();
    }

    public LoaderSynchronizationResult Synchronize(IReadOnlyList<PreparedBaselineScenario> records, string buildId)
    {
        if (records.Count == 0)
            return new(emptyRecords.Select(_ => (LoaderScenarioResult)null!).ToArray(), "Deletion skipped because the CSV contains no valid data rows.");

        var results = new List<LoaderScenarioResult>();
        var duplicateIds = records.GroupBy(record => record.NormalizedScenarioId, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToDictionary(group => group.Key, group => group.Select(record => record.CsvRow.RowNumber).ToArray(), StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (duplicateIds.TryGetValue(record.NormalizedScenarioId, out var rows))
            {
                results.Add(Failed(record, LoaderScenarioOutcome.DuplicateScenarioId, $"Duplicate Scenario_id '{record.NormalizedScenarioId}' at rows {string.Join(", ", rows)}."));
                continue;
            }
            results.Add(ProcessRecord(record, buildId));
        }

        // Obsolete deletion is deliberately deferred until every input row has succeeded.
        // A partial run must not remove valid baseline data.
        var deletionReason = string.Empty;
        if (results.Any(result => !result.Successful))
            deletionReason = "Deletion skipped because one or more scenarios failed.";
        else
        {
            try
            {
                var sourceIds = records.Select(record => record.NormalizedScenarioId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var obsoleteIds = repository.GetAllScenarioIds().Where(id => !sourceIds.Contains(id)).ToArray();
                if (obsoleteIds.Length > 0)
                {
                    repository.DeleteObsolete(new DeleteObsoleteScenarioCommand(obsoleteIds));
                    results.AddRange(obsoleteIds.Select(id => new LoaderScenarioResult { ScenarioId = id, Successful = true, Outcome = LoaderScenarioOutcome.Deleted, RequestTableAction = "Delete", ResponseTableAction = "Delete" }));
                }
            }
            catch (Exception exception)
            {
                deletionReason = $"Deletion skipped because the database operation failed: {exception.Message}";
                results.Add(new LoaderScenarioResult { ScenarioId = "__OBSOLETE_DELETION__", Successful = false, Outcome = LoaderScenarioOutcome.DatabaseFailed, Error = exception.Message });
            }
        }
        return new(results, deletionReason);
    }

    private LoaderScenarioResult ProcessRecord(PreparedBaselineScenario record, string buildId)
    {
        BaselineDatabaseSnapshot snapshot;
        try { snapshot = repository.GetByScenarioId(record.NormalizedScenarioId); }
        catch (Exception exception) { return Failed(record, LoaderScenarioOutcome.DatabaseFailed, exception.Message); }
        if (snapshot.RequestRows.Count > 1 || snapshot.ResponseRows.Count > 1)
            return Failed(record, LoaderScenarioOutcome.DatabaseConsistencyFailed, $"Scenario_id '{record.NormalizedScenarioId}' has {snapshot.RequestRows.Count} request rows and {snapshot.ResponseRows.Count} response rows.");

        try
        {
            var route = routeResolver.Resolve(record.RequestMetadata.SchemeCode);
            var existing = snapshot.RequestRows.SingleOrDefault();
            if (existing is null)
                return Insert(record, route, buildId);

            var existingFingerprint = fingerprintService.CreateFingerprint(metadataReader.Read(existing.XmlRequest).Document);
            // Fingerprints ignore harmless formatting changes while preserving the raw XML used for transport and storage.
            var xmlChanged = !string.Equals(existingFingerprint, record.XmlFingerprint, StringComparison.Ordinal);
            var tagsChanged = !existing.NormalizedTags.SequenceEqual(record.NormalizedTags, StringComparer.OrdinalIgnoreCase);
            if (!xmlChanged && !tagsChanged)
                return Success(record, LoaderScenarioOutcome.Unchanged, false, "None", "None");
            if (!xmlChanged)
            {
                repository.UpdateTags(record.NormalizedScenarioId, record.NormalizedTags);
                return Success(record, LoaderScenarioOutcome.TagsUpdated, false, "UpdateTags", "None");
            }
            return Update(record, route, buildId);
        }
        catch (LoaderRouteNotFoundException exception) { return Failed(record, LoaderScenarioOutcome.RouteNotFound, exception.Message); }
        catch (LoaderRouteDisabledException exception) { return Failed(record, LoaderScenarioOutcome.RouteDisabled, exception.Message); }
        catch (LoaderApiException exception) { evidenceWriter.Save(new(record.NormalizedScenarioId, record.CsvRow.XmlRequest, null, exception.Message)); return Failed(record, LoaderScenarioOutcome.ApiFailed, exception.Message); }
        catch (LoaderResponseValidationException exception) { evidenceWriter.Save(new(record.NormalizedScenarioId, record.CsvRow.XmlRequest, null, exception.Message)); return Failed(record, LoaderScenarioOutcome.ResponseValidationFailed, exception.Message); }
        catch (Exception exception) { return Failed(record, LoaderScenarioOutcome.DatabaseFailed, exception.Message); }
    }

    private LoaderScenarioResult Insert(PreparedBaselineScenario record, RouteDefinition route, string buildId)
    {
        // Validate the response before either baseline row is written.
        var response = apiClient.Fetch(new(record.NormalizedScenarioId, record.RequestMetadata.QuoteReference, record.CsvRow.XmlRequest), route);
        responseValidator.Validate(response, route);
        repository.Insert(new InsertBaselineScenarioCommand(record.NormalizedScenarioId, record.RequestMetadata.QuoteReference, record.CsvRow.XmlRequest, record.NormalizedTags, response.RawXml, buildId));
        return Success(record, LoaderScenarioOutcome.Inserted, true, "Insert", "Insert");
    }

    private LoaderScenarioResult Update(PreparedBaselineScenario record, RouteDefinition route, string buildId)
    {
        // Changed requests replace both stored XML values only after the new response passes validation.
        var response = apiClient.Fetch(new(record.NormalizedScenarioId, record.RequestMetadata.QuoteReference, record.CsvRow.XmlRequest), route);
        responseValidator.Validate(response, route);
        repository.Update(new UpdateBaselineScenarioCommand(record.NormalizedScenarioId, record.RequestMetadata.QuoteReference, record.CsvRow.XmlRequest, record.NormalizedTags, response.RawXml, buildId));
        return Success(record, LoaderScenarioOutcome.XmlUpdated, true, "Update", "Update");
    }

    private static LoaderScenarioResult Success(PreparedBaselineScenario record, LoaderScenarioOutcome outcome, bool apiCalled, string requestAction, string responseAction) => new() { ScenarioId = record.NormalizedScenarioId, QuoteRef = record.RequestMetadata.QuoteReference, Successful = true, Outcome = outcome, ApiCalled = apiCalled, RequestTableAction = requestAction, ResponseTableAction = responseAction };

    private static LoaderScenarioResult Failed(PreparedBaselineScenario record, LoaderScenarioOutcome outcome, string error) => new() { ScenarioId = record.NormalizedScenarioId, QuoteRef = record.RequestMetadata.QuoteReference, Successful = false, Outcome = outcome, Error = error };
}