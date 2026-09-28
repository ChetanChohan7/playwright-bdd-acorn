namespace PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;

using System.Globalization;

public class RadarUrlBuilder
{
	public string Build(string baseUrl, string routeKey, string requestTime)
	{
		if (string.IsNullOrWhiteSpace(baseUrl))
		{
			throw new ArgumentException("BaseUrl is required.", nameof(baseUrl));
		}

		if (string.IsNullOrWhiteSpace(routeKey))
		{
			throw new ArgumentException("RouteKey is required.", nameof(routeKey));
		}

		if (string.IsNullOrWhiteSpace(requestTime))
		{
			throw new ArgumentException("RequestTime is required.", nameof(requestTime));
		}

		var trimmedBaseUrl = baseUrl.Trim();
		var trimmedRouteKey = routeKey.Trim();
		var trimmedRequestTime = requestTime.Trim();
		if (!Uri.TryCreate(trimmedBaseUrl, UriKind.Absolute, out var absoluteUri))
		{
			throw new ArgumentException("BaseUrl must be a valid absolute URL.", nameof(baseUrl));
		}

		if (!DateTime.TryParseExact(
			trimmedRequestTime,
			"yyyy-MM-dd'Z'HH:mm:ss",
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out _))
		{
			throw new ArgumentException("RequestTime must use yyyy-MM-ddZHH:mm:ss format.", nameof(requestTime));
		}

		var encodedRouteKey = Uri.EscapeDataString(trimmedRouteKey);
		var encodedRequestTime = Uri.EscapeDataString(trimmedRequestTime);
		var separator = trimmedBaseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";

		return $"{trimmedBaseUrl}{separator}KeyName={encodedRouteKey}&KeyRequestTime={encodedRequestTime}";
	}
}
