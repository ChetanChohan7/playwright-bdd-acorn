using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Loader;

namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class IntegrationConfigurationValidator
{
    private readonly MatcherConfiguration configuration;

    public IntegrationConfigurationValidator(MatcherConfiguration configuration) => this.configuration = configuration;

    public void ValidateDatabase()
    {
        if (string.IsNullOrWhiteSpace(configuration.Database.SqlConnectionString) || IsMarker(configuration.Database.SqlConnectionString))
            throw new ConfigurationValidationException("Integration is not configured. Supply an approved SQL connection string through a protected setting.");
    }

    public void ValidateLoader(string csvPath, IReadOnlyList<PreparedBaselineScenario> records)
    {
        if (records.Count == 0)
            throw new ConfigurationValidationException("Loader integration is not configured. The authoritative CSV must contain at least one data row.");

        ValidateDatabase();
        foreach (var record in records)
            ValidateRoute(record.RequestMetadata.SchemeCode);
    }

    public void ValidateComparison(IEnumerable<string> schemeCodes)
    {
        ValidateDatabase();
        var codes = schemeCodes.ToArray();
        if (codes.Length == 0)
            throw new ConfigurationValidationException("Comparison integration is not configured. No scenarios matched the requested tags.");
        foreach (var schemeCode in codes)
            ValidateRoute(schemeCode);
    }

    public void ValidateEnabledRoutes()
    {
        var enabledRoutes = configuration.Routes.Where(pair => pair.Value.Enabled).Select(pair => pair.Key).ToArray();
        if (enabledRoutes.Length == 0)
            throw new ConfigurationValidationException("Comparison integration is not configured. Enable an approved production route before database scenario discovery.");
        foreach (var schemeCode in enabledRoutes)
            ValidateRoute(schemeCode);
    }

    public RouteSettings ValidateRoute(string schemeCode)
    {
        if (!configuration.Routes.TryGetValue(schemeCode, out var route) || !route.Enabled)
            throw new ConfigurationValidationException($"Comparison integration is not configured. Route for SchemeCode '{schemeCode}' is disabled or missing.");
        if (string.IsNullOrWhiteSpace(route.ResponseSchemaFile) || route.ResponseProcessorName.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationValidationException($"Route '{schemeCode}' requires a production XSD and response processor before integration can run.");
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", route.ResponseSchemaFile);
        if (!File.Exists(schemaPath))
            throw new ConfigurationValidationException($"Route '{schemeCode}' requires XSD '{route.ResponseSchemaFile}', which was not found.");
        if (!configuration.Endpoints.TryGetValue(route.EndpointName, out var endpoint) || !endpoint.Enabled)
            throw new ConfigurationValidationException($"Endpoint '{route.EndpointName}' for SchemeCode '{schemeCode}' is disabled or missing.");
        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var uri) || uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) || IsMarker(endpoint.BaseUrl))
            throw new ConfigurationValidationException($"Endpoint '{route.EndpointName}' requires an approved non-placeholder BaseUrl.");
        if (string.IsNullOrWhiteSpace(endpoint.Resource) || IsMarker(endpoint.Resource))
            throw new ConfigurationValidationException($"Endpoint '{route.EndpointName}' requires an API resource.");
        if (string.IsNullOrWhiteSpace(endpoint.Username) || string.IsNullOrWhiteSpace(endpoint.Password) || IsMarker(endpoint.Username) || IsMarker(endpoint.Password))
            throw new ConfigurationValidationException($"Endpoint '{route.EndpointName}' requires approved credentials supplied through protected configuration.");
        if (!string.IsNullOrWhiteSpace(configuration.Pipeline.ApiDate))
            _ = ExternalAPIAccess.ApiDateResolver.Resolve(configuration.Pipeline.ApiDate);
        return route;
    }

    private static bool IsMarker(string value) => value.Contains("__", StringComparison.Ordinal) || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}