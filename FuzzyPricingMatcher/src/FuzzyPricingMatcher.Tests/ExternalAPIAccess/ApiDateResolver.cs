using System.Globalization;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public static class ApiDateResolver
{
    public static DateOnly Resolve(string? configuredDate, DateTimeOffset? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(configuredDate))
            return DateOnly.FromDateTime((utcNow ?? DateTimeOffset.UtcNow).UtcDateTime);
        if (!DateOnly.TryParse(configuredDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ExternalAPIConfigurationException($"API date '{configuredDate}' is invalid.");
        return date;
    }
}