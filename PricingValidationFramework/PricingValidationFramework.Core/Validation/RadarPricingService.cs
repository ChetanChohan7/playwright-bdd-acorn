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

		var (target, targetFailure) = ResolveTarget(scenario);
		if (target is null)
		{
			return targetFailure!;
		}

		if (!comparisonService.HasProfile(scenario.ProductCode, scenario.SchemeCode))
		{
			return Failed(target.RouteId, string.Empty, "SchemaProfileResolution",
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
			return new RadarPricingRunResult(target.RouteId, string.Empty, result);
		}

		var (responseXml, callFailure) = await CallRadarAsync(scenario, target, requestTime, cancellationToken);
		if (responseXml is null)
		{
			return callFailure!;
		}

		var comparison = comparisonService.CompareExtractedBaseline(
			scenario.ProductCode,
			scenario.SchemeCode,
			baselineValidation.Document!,
			responseXml,
			minDelta,
			maxDelta);
		return new RadarPricingRunResult(target.RouteId, responseXml, comparison);
	}

	/// For a new scenario with no baseline yet: calls Radar and returns its response as a PASS,
	/// without XSD validation or the fuzzy comparison. The caller stores it as the baseline.
	public async Task<RadarPricingRunResult> CreateBaselineAsync(
		ScenarioRequest scenario,
		string requestTime,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenario);
		cancellationToken.ThrowIfCancellationRequested();

		var (target, targetFailure) = ResolveTarget(scenario);
		if (target is null)
		{
			return targetFailure!;
		}

		var (responseXml, callFailure) = await CallRadarAsync(scenario, target, requestTime, cancellationToken);
		if (responseXml is null)
		{
			return callFailure!;
		}

		return new RadarPricingRunResult(
			target.RouteId,
			responseXml,
			new PricingComparisonResult(
				Array.Empty<DecimalFieldComparison>(),
				failureStage: BaselineCreatedStage,
				result: ScenarioResult.Pass));
	}

	public const string BaselineCreatedStage = "BaselineCreated";

	private (RadarTarget? Target, RadarPricingRunResult? Failure) ResolveTarget(ScenarioRequest scenario)
	{
		var route = ResolveRoute(scenario);
		if (route is null)
		{
			return (null, Failed(string.Empty, string.Empty, "RouteResolution",
				$"Exactly one Radar route must match ProductCode '{scenario.ProductCode}' and SchemeCode '{scenario.SchemeCode}'."));
		}

		var (routeId, routeSettings) = route.Value;
		var endpoint = settings.Endpoints.FirstOrDefault(candidate =>
			string.Equals(candidate.Key, routeSettings.EndpointName, StringComparison.OrdinalIgnoreCase));
		if (endpoint.Value is null)
		{
			return (null, Failed(routeId, string.Empty, "EndpointResolution", $"Endpoint '{routeSettings.EndpointName}' is not configured."));
		}

		return (new RadarTarget(routeId, routeSettings, endpoint.Value), null);
	}

	private async Task<(string? ResponseXml, RadarPricingRunResult? Failure)> CallRadarAsync(
		ScenarioRequest scenario,
		RadarTarget target,
		string requestTime,
		CancellationToken cancellationToken)
	{
		try
		{
			var formattedRequestTime = RequestTimeFormatter.Resolve(requestTime);
			var url = urlBuilder.Build(target.Endpoint.BaseUrl, target.Route.RouteKey, formattedRequestTime);
			var responseXml = await apiClient.PostAsync(
				target.Route.EndpointName,
				url,
				target.Endpoint.ApiKeyHeaderName,
				target.Endpoint.ApiKeyValue,
				scenario.XmlRequest,
				cancellationToken,
				scenario.ScenarioId,
				scenario.QuoteRef,
				scenario.ProductCode,
				scenario.SchemeCode);
			return (responseXml, null);
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
			var safeError = string.IsNullOrEmpty(target.Endpoint.ApiKeyValue)
				? exception.Message
				: exception.Message.Replace(target.Endpoint.ApiKeyValue, "[REDACTED]", StringComparison.Ordinal);
			return (null, Failed(target.RouteId, string.Empty, "RequestExecution", safeError));
		}
	}

	private sealed record RadarTarget(string RouteId, RadarRouteSettings Route, RadarEndpointSettings Endpoint);

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