namespace PricingValidationFramework.Core.Validation;

using System.Net;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.ExternalAPIAccess.ApiClients;
using PricingValidationFramework.Core.ExternalAPIAccess.Throttling;
using PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;
using PricingValidationFramework.Core.Models.Database;
using PricingValidationFramework.Core.Models.Enums;
using PricingValidationFramework.Core.Models.Reporting;

public sealed class RadarPricingService
{
	private readonly RadarSettings settings;
	private readonly RadarApiClient apiClient;
	private readonly RadarUrlBuilder urlBuilder;
	private readonly PricingComparisonService comparisonService;

	public RadarPricingService(
		RadarSettings settings,
		RadarApiClient apiClient,
		RadarUrlBuilder urlBuilder,
		PricingComparisonService comparisonService)
	{
		this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
		this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
		this.urlBuilder = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
		this.comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
	}

	public async Task<RadarPricingRunResult> RunAsync(
		ScenarioRequest scenario,
		string baselineXml,
		string requestTime,
		decimal minDelta,
		decimal maxDelta,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenario);
		cancellationToken.ThrowIfCancellationRequested();

		var route = ResolveRoute(scenario);
		if (route is null)
		{
			return Failed(string.Empty, string.Empty, "RouteResolution",
				$"Exactly one Radar route must match ProductCode '{scenario.ProductCode}' and SchemeCode '{scenario.SchemeCode}'.");
		}

		var (routeId, routeSettings) = route.Value;
		var endpoint = settings.Endpoints.FirstOrDefault(candidate =>
			string.Equals(candidate.Key, routeSettings.EndpointName, StringComparison.OrdinalIgnoreCase));
		if (endpoint.Value is null)
		{
			return Failed(routeId, string.Empty, "EndpointResolution", $"Endpoint '{routeSettings.EndpointName}' is not configured.");
		}

		if (!comparisonService.HasProfile(scenario.ProductCode, scenario.SchemeCode))
		{
			return Failed(routeId, string.Empty, "SchemaProfileResolution",
				$"No pricing schema profile is configured for ProductCode '{scenario.ProductCode}' and SchemeCode '{scenario.SchemeCode}'.");
		}

		var baselineValidation = comparisonService.ValidateBaseline(
			scenario.ProductCode,
			scenario.SchemeCode,
			baselineXml);
		if (!baselineValidation.IsValid)
		{
			var result = new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				baselineValidation.Error,
				baselineValidation.FailureStage,
				schemaProfile: baselineValidation.SchemaProfile);
			return new RadarPricingRunResult(routeId, string.Empty, result);
		}

		string responseXml;
		try
		{
			var formattedRequestTime = RequestTimeFormatter.Resolve(requestTime);
			var url = urlBuilder.Build(endpoint.Value.BaseUrl, routeSettings.RouteKey, formattedRequestTime);
			responseXml = await apiClient.PostAsync(
				routeSettings.EndpointName,
				url,
				endpoint.Value.ApiKeyHeaderName,
				endpoint.Value.ApiKeyValue,
				scenario.XmlRequest,
				cancellationToken,
				scenario.ScenarioId,
				scenario.QuoteRef,
				scenario.ProductCode,
				scenario.SchemeCode);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (RadarRequestRateLimitException)
		{
			throw;
		}
		catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
		{
			throw;
		}
		catch (Exception exception)
		{
			var safeError = string.IsNullOrEmpty(endpoint.Value.ApiKeyValue)
				? exception.Message
				: exception.Message.Replace(endpoint.Value.ApiKeyValue, "[REDACTED]", StringComparison.Ordinal);
			return Failed(routeId, string.Empty, "RequestExecution", safeError);
		}

		var comparison = comparisonService.CompareExtractedBaseline(
			scenario.ProductCode,
			scenario.SchemeCode,
			baselineValidation.Document!,
			responseXml,
			minDelta,
			maxDelta);
		return new RadarPricingRunResult(routeId, responseXml, comparison);
	}

	private (string Key, RadarRouteSettings Value)? ResolveRoute(ScenarioRequest scenario)
	{
		var productCode = scenario.ProductCode.Trim();
		var schemeCode = scenario.SchemeCode.Trim();
		var matching = settings.Routes.Where(route =>
			string.Equals(route.Value.ProductCode, productCode, StringComparison.Ordinal) &&
			route.Value.SchemeCodes.Any(configuredScheme =>
				string.Equals(configuredScheme, schemeCode, StringComparison.Ordinal))).ToArray();

		return matching.Length switch
		{
			1 => (matching[0].Key, matching[0].Value),
			0 => null,
			_ => null
		};
	}

	private static RadarPricingRunResult Failed(string routeId, string responseXml, string stage, string error)
	{
		return new RadarPricingRunResult(
			routeId,
			responseXml,
			new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				error,
				stage,
				ScenarioResult.Error));
	}
}