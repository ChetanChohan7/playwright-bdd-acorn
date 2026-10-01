namespace PricingValidationFramework.Core.ExternalAPIAccess;

using System.Net;

/// Shared between IceApiClient and RadarApiClient: which HTTP statuses are worth retrying, and
/// the exponential backoff used when retrying locally (as opposed to Radar's additional
/// Retry-After-aware delay, which stays Radar-specific).
internal static class HttpRetryPolicy
{
	public static bool IsSuccess(HttpStatusCode statusCode) => (int)statusCode is >= 200 and <= 299;

	public static bool IsTransient(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or
			HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
			HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

	public static void EnsureSuccessStatusCode(HttpResponseMessage response, string clientName)
	{
		if (IsSuccess(response.StatusCode))
		{
			return;
		}

		throw new HttpRequestException(
			$"{clientName} request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
			null,
			response.StatusCode);
	}

	public static TimeSpan GetExponentialDelay(int attempt, int baseDelaySeconds) =>
		TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt));
}
