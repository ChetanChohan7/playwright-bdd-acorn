using System.Globalization;
using FuzzyPricingMatcher.Tests.Configuration;

namespace FuzzyPricingMatcher.Tests.Api;

public sealed class ApiResourceBuilder : IApiResourceBuilder
{
    public ApiResource Build(EndpointSettings endpoint, DateOnly apiDate)
    {
        ValidateEndpoint(endpoint);
        var placement = ParseDatePlacement(endpoint.DatePlacement);
        var formattedDate = FormatDate(apiDate, endpoint.DateFormat);

        return placement == DatePlacement.Path
            ? BuildPathResource(endpoint.Resource, formattedDate)
            : new ApiResource(endpoint.Resource, endpoint.DateParameterName, formattedDate);
    }

    private static void ValidateEndpoint(EndpointSettings endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Resource))
            throw new ApiConfigurationException("API resource is required.");
        if (string.IsNullOrWhiteSpace(endpoint.DateParameterName))
            throw new ApiConfigurationException("Date parameter name is required.");
        if (string.IsNullOrWhiteSpace(endpoint.DateFormat))
            throw new ApiConfigurationException("Date format is required.");
    }

    private static DatePlacement ParseDatePlacement(string value)
    {
        if (!Enum.TryParse<DatePlacement>(value, true, out var placement))
            throw new ApiConfigurationException($"Unsupported date placement '{value}'.");
        return placement;
    }

    private static string FormatDate(DateOnly apiDate, string dateFormat)
    {
        string formattedDate;
        try { formattedDate = apiDate.ToString(dateFormat, CultureInfo.InvariantCulture); }
        catch (FormatException) { throw new ApiConfigurationException($"Invalid API date format '{dateFormat}'."); }
        if (formattedDate.Contains("/") || formattedDate.Contains("\\"))
            throw new ApiConfigurationException("API date format produced an unsafe resource value.");
        return formattedDate;
    }

    private static ApiResource BuildPathResource(string resource, string formattedDate)
    {
        var escapedDate = Uri.EscapeDataString(formattedDate);
        var resourcePath = resource.Contains("{date}", StringComparison.OrdinalIgnoreCase)
            ? resource.Replace("{date}", escapedDate, StringComparison.OrdinalIgnoreCase)
            : $"{resource.TrimEnd('/')}/{escapedDate}";
        return new ApiResource(resourcePath, null, null);
    }

    private enum DatePlacement
    {
        Path,
        QueryString
    }
}