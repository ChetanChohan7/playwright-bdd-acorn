using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Logging;
using PricingValidationFramework.Core.Matching;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.External;
using PricingValidationFramework.Core.Models.Reporting;
using PricingValidationFramework.Core.Validation;
using PricingValidationFramework.Tests.Helpers.Reporting;
using System.Xml.Serialization;

namespace PricingValidationFramework.Tests.Helpers.Validation;

public sealed class RadarScenarioProcessor
{
    private static readonly XmlSerializer responseSerializer = new(typeof(RadarResponse));
    private readonly RadarSettings radarSettings;
    private readonly RadarApiClient apiClient;
    private readonly RadarUrlBuilder urlBuilder;
    private readonly XsdFileResolver xsdFileResolver;
    private readonly XsdValidator xsdValidator;
    private readonly ThresholdMatcher thresholdMatcher;
    private readonly RadarTestRunLogger logger;

    public RadarScenarioProcessor(
        RadarSettings radarSettings,
        RadarApiClient apiClient,
        RadarUrlBuilder urlBuilder,
        XsdFileResolver xsdFileResolver,
        XsdValidator xsdValidator,
        ThresholdMatcher thresholdMatcher,
        RadarTestRunLogger logger)
    {
        this.radarSettings = radarSettings ?? throw new ArgumentNullException(nameof(radarSettings));
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.urlBuilder = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
        this.xsdFileResolver = xsdFileResolver ?? throw new ArgumentNullException(nameof(xsdFileResolver));
        this.xsdValidator = xsdValidator ?? throw new ArgumentNullException(nameof(xsdValidator));
        this.thresholdMatcher = thresholdMatcher ?? throw new ArgumentNullException(nameof(thresholdMatcher));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RadarValidationReportRow> ProcessAsync(
        ScenarioRequest scenario,
        string buildId,
        decimal baselineValue,
        decimal minThreshold,
        decimal maxThreshold,
        string requestTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        cancellationToken.ThrowIfCancellationRequested();

        var matchingRoute = ResolveRoute(scenario);
        if (matchingRoute is null)
        {
            return BuildErrorRow(scenario, buildId, string.Empty, baselineValue, minThreshold, maxThreshold);
        }

        var routeIdentifier = matchingRoute.Value.Key;
        var routeSettings = matchingRoute.Value.Value;
        var endpointSettings = ResolveEndpoint(scenario, routeIdentifier, routeSettings);
        if (endpointSettings is null)
        {
            return BuildErrorRow(scenario, buildId, string.Empty, baselineValue, minThreshold, maxThreshold);
        }

        return await ProcessRoutedScenarioAsync(
            scenario,
            routeIdentifier,
            routeSettings,
            endpointSettings,
            buildId,
            baselineValue,
            minThreshold,
            maxThreshold,
            requestTime,
            cancellationToken);
    }

    private KeyValuePair<string, RadarRouteSettings>? ResolveRoute(ScenarioRequest scenario)
    {
        var trimmedProductCode = scenario.ProductCode.Trim();
        var trimmedSchemeCode = scenario.SchemeCode.Trim();
        var matchingRoutes = radarSettings.Routes
            .Where(route =>
                string.Equals(route.Value.ProductCode, trimmedProductCode, StringComparison.Ordinal) &&
                string.Equals(route.Value.SchemeCode, trimmedSchemeCode, StringComparison.Ordinal))
            .ToList();

        if (matchingRoutes.Count == 0)
        {
            RecordError(
                scenario,
                $"Radar route for ProductCode '{trimmedProductCode}' and SchemeCode '{trimmedSchemeCode}' was not found.",
                executionStage: "RouteResolution");
            return null;
        }

        if (matchingRoutes.Count > 1)
        {
            RecordError(
                scenario,
                $"Multiple Radar routes matched ProductCode '{trimmedProductCode}' and SchemeCode '{trimmedSchemeCode}'.",
                executionStage: "RouteResolution");
            return null;
        }

        return matchingRoutes[0];
    }

    private RadarEndpointSettings? ResolveEndpoint(
        ScenarioRequest scenario,
        string routeIdentifier,
        RadarRouteSettings routeSettings)
    {
        var endpointSettings = radarSettings.Endpoints
            .FirstOrDefault(endpoint => string.Equals(
                endpoint.Key,
                routeSettings.EndpointName,
                StringComparison.OrdinalIgnoreCase))
            .Value;
        if (endpointSettings is null)
        {
            RecordError(
                scenario,
                $"Radar endpoint '{routeSettings.EndpointName}' was not found for configured route '{routeIdentifier}'.",
                routeSettings.EndpointName,
                "EndpointResolution");
        }

        return endpointSettings;
    }

    private async Task<RadarValidationReportRow> ProcessRoutedScenarioAsync(
        ScenarioRequest scenario,
        string routeIdentifier,
        RadarRouteSettings routeSettings,
        RadarEndpointSettings endpointSettings,
        string buildId,
        decimal baselineValue,
        decimal minThreshold,
        decimal maxThreshold,
        string requestTime,
        CancellationToken cancellationToken)
    {
        var radarResponseXml = string.Empty;
        var executionStage = "RequestExecution";
        try
        {
            radarResponseXml = await ExecuteRequestAsync(
                scenario,
                routeSettings,
                endpointSettings,
                requestTime,
                cancellationToken);

            var result = ValidateAndBuildResult(
                scenario,
                routeIdentifier,
                routeSettings,
                endpointSettings.ApiKeyValue,
                buildId,
                radarResponseXml,
                baselineValue,
                minThreshold,
                maxThreshold,
                ref executionStage);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception ex) when (
            ex is OperationCanceledException or RadarRequestRateLimitException ||
            ex is HttpRequestException { StatusCode: global::System.Net.HttpStatusCode.TooManyRequests })
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordError(
                scenario,
                Redact(ex.Message, endpointSettings.ApiKeyValue),
                routeSettings.EndpointName,
                executionStage,
                endpointSettings.ApiKeyValue);
            return BuildErrorRow(scenario, buildId, radarResponseXml, baselineValue, minThreshold, maxThreshold);
        }
    }

    private RadarValidationReportRow ValidateAndBuildResult(
        ScenarioRequest scenario,
        string routeIdentifier,
        RadarRouteSettings routeSettings,
        string apiKeyValue,
        string buildId,
        string radarResponseXml,
        decimal baselineValue,
        decimal minThreshold,
        decimal maxThreshold,
        ref string executionStage)
    {
        // TODO: Register each received response schema in RadarSettings.ResponseXsdMappings under its route identifier.
        var responseXsdFile = radarSettings.ResponseXsdMappings.TryGetValue(routeIdentifier, out var mappedXsdFile)
            ? mappedXsdFile
            : string.Empty;
        var responseValidation = radarSettings.ValidateResponseXsd
            ? ValidateResponseXml(radarResponseXml, responseXsdFile, ref executionStage)
            : null;
        if (responseValidation is { IsValid: false })
        {
            RecordError(
                scenario,
                $"Radar response XSD validation failed. Errors: {string.Join(" | ", responseValidation.Errors)}",
                routeSettings.EndpointName,
                "XsdValidation",
                apiKeyValue);
            executionStage = "ResultConstruction";
            return BuildValidationFailureRow(scenario, buildId, radarResponseXml, baselineValue, minThreshold, maxThreshold);
        }

        executionStage = "ResponseDeserialization";
        var radarResponse = DeserializeResponse(radarResponseXml);
        var radarValue = radarResponse.TotalAmount;

        executionStage = "BaselineComparison";
        var difference = radarValue - baselineValue;
        var result = thresholdMatcher.IsWithinThreshold(difference, minThreshold, maxThreshold)
            ? ScenarioResult.Pass
            : ScenarioResult.Fail;

        executionStage = "ResultConstruction";
        return RadarReportingHelper.BuildRow(
            buildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            radarResponseXml,
            radarValue,
            baselineValue,
            difference,
            minThreshold,
            maxThreshold,
            result);
    }

    private async Task<string> ExecuteRequestAsync(
        ScenarioRequest scenario,
        RadarRouteSettings routeSettings,
        RadarEndpointSettings endpointSettings,
        string requestTime,
        CancellationToken cancellationToken)
    {
        var formattedRequestTime = RequestTimeFormatter.Resolve(requestTime);
        var url = urlBuilder.Build(endpointSettings.BaseUrl, routeSettings.RouteKey, formattedRequestTime);
        return await apiClient.PostAsync(
            routeSettings.EndpointName,
            url,
            endpointSettings.ApiKeyHeaderName,
            endpointSettings.ApiKeyValue,
            scenario.XmlRequest,
            cancellationToken,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.ProductCode,
            scenario.SchemeCode);
    }

    private XsdValidationResult? ValidateResponseXml(
        string responseXml,
        string? xsdFile,
        ref string executionStage)
    {
        executionStage = "XsdResolution";
        var resolvedXsdPath = string.IsNullOrWhiteSpace(xsdFile)
            ? null
            : xsdFileResolver.Resolve(xsdFile);

        executionStage = "XsdValidation";
        return resolvedXsdPath is null
            ? null
            : xsdValidator.Validate(responseXml, resolvedXsdPath);
    }

    private static RadarResponse DeserializeResponse(string responseXml)
    {
        // TODO: Expand the typed response (XML now, JSON too if the client ends up using it) once the schema/contract is confirmed.
        using var reader = new StringReader(responseXml);
        return (RadarResponse?)responseSerializer.Deserialize(reader)
            ?? throw new InvalidDataException("Radar response XML did not contain a response document.");
    }

    private void RecordError(
        ScenarioRequest scenario,
        string reason,
        string? endpointName = null,
        string executionStage = "ScenarioProcessing",
        string? apiKeyValue = null)
    {
        var safeReason = Redact(reason, apiKeyValue);
        logger.ExecutionFailed(
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.ProductCode,
            scenario.SchemeCode,
            endpointName,
            executionStage,
            new InvalidOperationException(safeReason),
            safeDetail: safeReason);
    }

    private static string Redact(string reason, string? apiKeyValue)
    {
        if (string.IsNullOrEmpty(apiKeyValue))
        {
            return reason;
        }

        return reason.Replace(apiKeyValue, "[REDACTED]", StringComparison.Ordinal);
    }

    private static RadarValidationReportRow BuildErrorRow(
        ScenarioRequest scenario,
        string buildId,
        string radarResponseXml,
        decimal baselineValue,
        decimal minThreshold,
        decimal maxThreshold)
    {
        return RadarReportingHelper.BuildRow(
            buildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            radarResponseXml,
            null,
            baselineValue,
            null,
            minThreshold,
            maxThreshold,
            ScenarioResult.Error);
    }

    private static RadarValidationReportRow BuildValidationFailureRow(
        ScenarioRequest scenario,
        string buildId,
        string radarResponseXml,
        decimal baselineValue,
        decimal minThreshold,
        decimal maxThreshold)
    {
        return RadarReportingHelper.BuildRow(
            buildId,
            scenario.ScenarioId,
            scenario.QuoteRef,
            scenario.SchemeCode,
            scenario.ProductCode,
            scenario.XmlRequest,
            radarResponseXml,
            null,
            baselineValue,
            null,
            minThreshold,
            maxThreshold,
            ScenarioResult.Fail);
    }
}