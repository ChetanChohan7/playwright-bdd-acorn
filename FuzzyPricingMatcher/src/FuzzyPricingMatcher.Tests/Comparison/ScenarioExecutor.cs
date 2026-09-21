using FuzzyPricingMatcher.Tests.ExternalAPIAccess;
using FuzzyPricingMatcher.Tests.Database;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Validation;

namespace FuzzyPricingMatcher.Tests.Comparison;

public sealed class ComparisonScenarioExecutor
{
    private readonly IFuzzyMatcherRepository repository;
    private readonly RequestXmlMetadataReader metadataReader;
    private readonly IScenarioRouteResolver routeResolver;
    private readonly IExternalXmlServiceClient apiClient;
    private readonly ExternalResponseValidationService validationService;
    private readonly ThresholdEvaluator thresholdEvaluator;
    private readonly IScenarioEvidenceWriter evidenceWriter;
    private readonly IScenarioLogger logger;

    public ComparisonScenarioExecutor(IFuzzyMatcherRepository repository, RequestXmlMetadataReader metadataReader, IScenarioRouteResolver routeResolver, IExternalXmlServiceClient apiClient, ExternalResponseValidationService validationService, ThresholdEvaluator thresholdEvaluator, IScenarioEvidenceWriter evidenceWriter, IScenarioLogger logger)
    {
        this.repository = repository;
        this.metadataReader = metadataReader;
        this.routeResolver = routeResolver;
        this.apiClient = apiClient;
        this.validationService = validationService;
        this.thresholdEvaluator = thresholdEvaluator;
        this.evidenceWriter = evidenceWriter;
        this.logger = logger;
    }

    public ComparisonExecutionResult Execute(ComparisonScenario scenario, ComparisonScenarioInput input, CancellationToken cancellationToken = default)
    {
        DatabaseScenarioSnapshot baselineSnapshot;
        try { baselineSnapshot = repository.GetScenarioForComparisonAsync(scenario.ScenarioId, cancellationToken).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { return Finish(scenario, ComparisonOutcome.Cancelled, false, null, null, null, "Comparison was cancelled.", null, null); }
        catch (DatabaseConsistencyException exception) { return Finish(scenario, ComparisonOutcome.DatabaseConsistencyFailed, false, null, null, null, exception.Message, null, null); }
        catch (Exception exception) { return Finish(scenario, ComparisonOutcome.DatabaseFailed, false, null, null, null, exception.Message, null, null); }
        if (baselineSnapshot.Request is null && baselineSnapshot.Response is null)
            return Finish(scenario, ComparisonOutcome.MissingBaseline, false, null, null, null, "Comparison cannot proceed because both the request and response baseline are missing. Run or repair the Baseline Loader.", null, null);
        if (baselineSnapshot.Response is null)
            return Finish(scenario, ComparisonOutcome.MissingBaseline, false, null, null, null, "Comparison cannot proceed because the response baseline is missing. No failure status could be persisted; run or repair the Baseline Loader.", baselineSnapshot.Request?.XmlRequest, null);
        if (baselineSnapshot.Request is null)
            return FinishFailure(scenario, input, ComparisonOutcome.MissingBaseline, null, null, null, "Comparison cannot proceed because the request baseline is missing. The unique response row was marked Fail; run or repair the Baseline Loader.", null, null, cancellationToken);

        var storedRequest = baselineSnapshot.Request;
        var storedBaselineResponse = baselineSnapshot.Response;
        ScenarioEvidence? evidence = null;
        try
        {
            // Evidence starts before the API call so failures retain the original request,
            // stored baseline, and any response received before validation fails.
            evidence = new ScenarioEvidence(scenario.ScenarioId, scenario.QuoteRef, storedRequest.XmlRequest, storedBaselineResponse.XmlResponse, null, "Started", string.Empty);
            evidenceWriter.Save(evidence);
            var requestMetadata = metadataReader.Read(storedRequest.XmlRequest);
            if (!string.Equals(requestMetadata.QuoteReference, storedBaselineResponse.QuoteRef, StringComparison.Ordinal))
                logger.QuoteMismatch(scenario.ScenarioId, requestMetadata.QuoteReference, storedBaselineResponse.QuoteRef);
            var selectedSchemeRoute = routeResolver.Resolve(requestMetadata.SchemeCode);
            var apiDate = ApiDateResolver.Resolve(input.ApiDate);
            var currentApiRequest = new ExternalXmlRequest(scenario.ScenarioId, requestMetadata.SchemeCode, storedRequest.XmlRequest, input.BuildId, selectedSchemeRoute.Endpoint, selectedSchemeRoute.Route, apiDate);
            var currentApiResponse = apiClient.SendXmlRequestAsync(currentApiRequest, cancellationToken).GetAwaiter().GetResult();
            evidence = evidence with { RawApiXml = currentApiResponse.ResponseXml };
            if (!currentApiResponse.Successful)
                return FinishFailure(scenario, input, ComparisonOutcome.ApiFailed, null, null, null, currentApiResponse.Error, storedRequest.XmlRequest, currentApiResponse.ResponseXml, cancellationToken);
            decimal currentApiAmount;
            try { currentApiAmount = validationService.ValidateAndReadAmount(new XmlResponseValidationRequest(currentApiResponse.ResponseXml, scenario.ScenarioId, requestMetadata.QuoteReference, "API response", selectedSchemeRoute.Route.ResponseSchemaFile), selectedSchemeRoute.Route.ResponseProcessorName).Amount; }
            catch (Exception exception) { return FinishFailure(scenario, input, ComparisonOutcome.ApiValidationFailed, null, null, null, exception.Message, storedRequest.XmlRequest, currentApiResponse.ResponseXml, cancellationToken); }
            decimal storedBaselineAmount;
            try { storedBaselineAmount = validationService.ValidateAndReadAmount(new XmlResponseValidationRequest(storedBaselineResponse.XmlResponse, scenario.ScenarioId, requestMetadata.QuoteReference, "stored baseline", selectedSchemeRoute.Route.ResponseSchemaFile), selectedSchemeRoute.Route.ResponseProcessorName).Amount; }
            catch (Exception exception) { return FinishFailure(scenario, input, ComparisonOutcome.BaselineValidationFailed, currentApiAmount, null, null, exception.Message, storedRequest.XmlRequest, currentApiResponse.ResponseXml, cancellationToken); }
            ThresholdResult comparisonThreshold;
            try { comparisonThreshold = thresholdEvaluator.Evaluate(currentApiAmount, storedBaselineAmount, input.MinimumThreshold, input.MaximumThreshold); }
            catch (Exception exception) { return FinishFailure(scenario, input, ComparisonOutcome.ExtractionFailed, currentApiAmount, storedBaselineAmount, null, exception.Message, storedRequest.XmlRequest, currentApiResponse.ResponseXml, cancellationToken); }
            if (comparisonThreshold.Passed)
            {
                // Persist the new response before returning a passing NUnit result.
                repository.UpdateComparisonPassAsync(new DatabaseComparisonPassCommand(scenario.ScenarioId, currentApiResponse.ResponseXml, input.BuildId), cancellationToken).GetAwaiter().GetResult();
                var comparisonResult = new ComparisonResult(scenario.ScenarioId, requestMetadata.QuoteReference, ComparisonOutcome.Passed, true, currentApiAmount, storedBaselineAmount, comparisonThreshold.Difference, string.Empty);
                logger.Outcome(comparisonResult);
                evidenceWriter.Save(evidence with { RawApiXml = currentApiResponse.ResponseXml, Outcome = comparisonResult.Outcome.ToString() });
                return new ComparisonExecutionResult(comparisonResult, true, true);
            }
            return FinishFailure(scenario, input, ComparisonOutcome.ThresholdFailed, currentApiAmount, storedBaselineAmount, comparisonThreshold.Difference, "Difference was outside the inclusive threshold range.", storedRequest.XmlRequest, currentApiResponse.ResponseXml, cancellationToken, requestMetadata.QuoteReference);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Finish(scenario, ComparisonOutcome.Cancelled, false, null, null, null, "Comparison was cancelled.", storedRequest.XmlRequest, evidence?.RawApiXml);
        }
        catch (Exception exception)
        {
            var failed = new ComparisonResult(scenario.ScenarioId, scenario.QuoteRef, ComparisonOutcome.DatabaseFailed, false, null, null, null, exception.Message);
            logger.Outcome(failed);
            if (evidence is not null) evidenceWriter.Save(evidence with { Outcome = failed.Outcome.ToString(), Error = failed.Error });
            return FinishFailure(scenario, input, ComparisonOutcome.DatabaseFailed, null, null, null, exception.Message, storedRequest.XmlRequest, evidence?.RawApiXml, cancellationToken);
        }
    }

    private ComparisonExecutionResult FinishFailure(ComparisonScenario scenario, ComparisonScenarioInput input, ComparisonOutcome outcome, decimal? api, decimal? baseline, decimal? difference, string error, string? rawRequest, string? rawApi, CancellationToken cancellationToken, string? quoteRef = null)
    {
        var updated = false;
        try
        {
            repository.UpdateComparisonFailAsync(new DatabaseComparisonFailCommand(scenario.ScenarioId, input.BuildId), cancellationToken).GetAwaiter().GetResult();
            updated = true;
        }
        catch (Exception exception)
        {
            outcome = ComparisonOutcome.DatabaseFailed;
            error = $"Comparison result could not be saved: {exception.Message}. Original failure: {error}";
        }
        return Finish(scenario, outcome, updated, api, baseline, difference, error, rawRequest, rawApi, quoteRef);
    }

    private ComparisonExecutionResult Finish(ComparisonScenario scenario, ComparisonOutcome outcome, bool updated, decimal? api, decimal? baseline, decimal? difference, string error, string? rawRequest, string? rawApi, string? quoteRef = null)
    {
        var result = new ComparisonResult(scenario.ScenarioId, quoteRef ?? scenario.QuoteRef, outcome, false, api, baseline, difference, error);
        logger.Outcome(result);
        if (rawRequest is not null) evidenceWriter.Save(new ScenarioEvidence(scenario.ScenarioId, scenario.QuoteRef, rawRequest, string.Empty, rawApi, outcome.ToString(), error));
        return new ComparisonExecutionResult(result, updated, rawRequest is not null);
    }
}