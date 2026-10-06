namespace PricingValidationFramework.Core.ExternalAPIAccess.UrlBuilders;

using System.Globalization;

public static class RequestTimeFormatter
{
	private static string FormatUtcHour(DateTime value)
	{
		var startOfHour = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
		return startOfHour.ToString("yyyy-MM-dd'Z'HH:mm:ss", CultureInfo.InvariantCulture);
	}
	public static string FormatUtcNow()
	{
		return FormatUtcHour(DateTime.UtcNow);
	}

	public static string Resolve(string? suppliedValue)
	{
		if (string.IsNullOrWhiteSpace(suppliedValue))
		{
			return FormatUtcNow();
		}

		var trimmedValue = suppliedValue.Trim();
		if (!DateTime.TryParseExact(
			trimmedValue,
			"yyyy-MM-dd'Z'HH:mm:ss",
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
			out var parsed))
		{
			throw new ArgumentException("RequestTime must use yyyy-MM-ddZHH:mm:ss format.", nameof(suppliedValue));
		}

		return FormatUtcHour(parsed.ToUniversalTime());
	}
}
