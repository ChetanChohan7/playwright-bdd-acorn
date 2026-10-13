namespace PricingValidationFramework.Core.Configuration;

/// Shared by the Radar tests and the DataLoader seed command, so both reject the same bad
/// endpoint and route settings before any scenario is read or Radar is called.
public static class RadarSettingsValidator
{
	/// Drops endpoints whose BaseUrl, header name and key are all empty (unused pipeline variables, or
	/// placeholders left blank in a local settings file), so only the endpoints in use need values.
	/// A partly filled endpoint is kept, and Validate rejects it.
	public static void RemoveBlankEndpoints(RadarSettings radarSettings)
	{
		ArgumentNullException.ThrowIfNull(radarSettings);
		radarSettings.Endpoints = (radarSettings.Endpoints ?? []).Where(endpoint => endpoint.Value is not null &&
				!(string.IsNullOrWhiteSpace(endpoint.Value.BaseUrl) &&
				  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyHeaderName) &&
				  string.IsNullOrWhiteSpace(endpoint.Value.ApiKeyValue)))
			.ToDictionary(endpoint => endpoint.Key, endpoint => endpoint.Value, StringComparer.Ordinal);
	}

	/// <param name="requireEndpointForEveryRoute">
	/// True requires every route's endpoint to be configured. False (Radar runs and seeding) lets a
	/// route name an endpoint that isn't configured; its scenarios fail with EndpointResolution
	/// instead, so a run can configure only the endpoints it needs.
	/// </param>
	public static void Validate(
		RadarSettings radarSettings,
		RadarRateLimitSettings rateLimitSettings,
		bool requireEndpointForEveryRoute = true)
	{
		ArgumentNullException.ThrowIfNull(radarSettings);
		ArgumentNullException.ThrowIfNull(rateLimitSettings);
		rateLimitSettings.Validate();

		if (radarSettings.Endpoints is null || radarSettings.Endpoints.Count == 0)
		{
			throw new InvalidOperationException("At least one Radar logical endpoint must be configured.");
		}

		var endpointNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var endpointName in radarSettings.Endpoints.Keys)
		{
			if (string.IsNullOrWhiteSpace(endpointName))
			{
				throw new InvalidOperationException("Radar logical endpoint names must not be empty.");
			}

			if (endpointName.Any(character =>
				!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.')))
			{
				throw new InvalidOperationException("Radar logical endpoint names contain unsupported characters.");
			}

			if (!endpointNames.Add(endpointName))
			{
				throw new InvalidOperationException("Radar logical endpoint names must be unique ignoring case.");
			}

			var endpoint = radarSettings.Endpoints[endpointName];
			if (endpoint is null ||
				!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var baseUri) ||
				(baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an absolute HTTP or HTTPS BaseUrl.");
			}

			if (string.IsNullOrWhiteSpace(endpoint.ApiKeyHeaderName))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an ApiKeyHeaderName.");
			}

			if (string.IsNullOrWhiteSpace(endpoint.ApiKeyValue))
			{
				throw new InvalidOperationException($"Radar endpoint '{endpointName}' must have an ApiKeyValue.");
			}
		}

		if (radarSettings.Routes is null)
		{
			throw new InvalidOperationException("Radar routes are missing.");
		}

		if (radarSettings.Routes.Count == 0)
		{
			throw new InvalidOperationException("At least one Radar route must be configured.");
		}

		foreach (var route in radarSettings.Routes)
		{
			if (string.IsNullOrWhiteSpace(route.Value?.ProductCode) ||
				route.Value.SchemeCodes is null ||
				route.Value.SchemeCodes.Count == 0 ||
				route.Value.SchemeCodes.Any(string.IsNullOrWhiteSpace))
			{
				throw new InvalidOperationException("Every Radar route must have a ProductCode and at least one non-empty SchemeCode.");
			}

			if (string.IsNullOrWhiteSpace(route.Value.EndpointName) ||
				(requireEndpointForEveryRoute && !endpointNames.Contains(route.Value.EndpointName)))
			{
				throw new InvalidOperationException("Every Radar route must reference a configured logical endpoint.");
			}

			if (string.IsNullOrWhiteSpace(route.Value.RouteKey))
			{
				throw new InvalidOperationException("Every Radar route must have a RouteKey.");
			}
		}
	}
}
