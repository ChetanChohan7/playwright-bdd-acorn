using Microsoft.Extensions.Configuration;

namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class MatcherConfiguration
{
    public AutomationSettings Automation { get; init; } = new();
    public DatabaseSettings Database { get; init; } = new();
    public PipelineRunContext Pipeline { get; init; } = new();
    public LoaderSettings Loader { get; init; } = new();
    public EvidenceSettings Evidence { get; init; } = new();
    public ResilienceSettings Resilience { get; init; } = new();
    public Dictionary<string, EndpointSettings> Endpoints { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RouteSettings> Routes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static MatcherConfiguration Load(string basePath)
    {
        return Bind(new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build());
    }

    public static MatcherConfiguration LoadStandalone(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Bind(new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(fullPath)!)
            .AddJsonFile(Path.GetFileName(fullPath), optional: false)
            .Build());
    }

    private static MatcherConfiguration Bind(IConfiguration configuration)
    {
        var result = new MatcherConfiguration();
        configuration.Bind(result);
        return result;
    }

    public EndpointSettings GetEnabledEndpoint(string endpointName)
    {
        if (!Endpoints.TryGetValue(endpointName, out var endpoint))
            throw new ConfigurationValidationException($"Unknown endpoint '{endpointName}'.");

        ValidateEndpoint(endpointName, endpoint);
        return endpoint;
    }

    public RouteSettings GetEnabledRoute(string schemeCode)
    {
        if (!Routes.TryGetValue(schemeCode, out var route))
            throw new ConfigurationValidationException($"Unknown SchemeCode '{schemeCode}'.");
        if (!route.Enabled)
            throw new ConfigurationValidationException($"Route '{schemeCode}' is disabled.");
        if (string.IsNullOrWhiteSpace(route.EndpointName))
            throw new ConfigurationValidationException($"Route '{schemeCode}' has no endpoint.");
        GetEnabledEndpoint(route.EndpointName);
        if (string.IsNullOrWhiteSpace(route.ResponseSchemaFile))
            throw new ConfigurationValidationException($"Route '{schemeCode}' has no response schema file.");
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Schemas", route.ResponseSchemaFile)))
            throw new ConfigurationValidationException($"Response schema '{route.ResponseSchemaFile}' was not found.");
        if (string.IsNullOrWhiteSpace(route.ResponseProcessorName))
            throw new ConfigurationValidationException($"Route '{schemeCode}' has no response processor.");
        return route;
    }

    private static void ValidateEndpoint(string endpointName, EndpointSettings endpoint)
    {
        if (!endpoint.Enabled)
            throw new ConfigurationValidationException($"Endpoint '{endpointName}' is disabled.");
        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var uri) || uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            throw new ConfigurationValidationException($"Endpoint '{endpointName}' does not have a permitted live host.");
        if (string.IsNullOrWhiteSpace(endpoint.Resource))
            throw new ConfigurationValidationException($"Endpoint '{endpointName}' has no resource.");
        if (string.IsNullOrWhiteSpace(endpoint.Username))
            throw new ConfigurationValidationException($"Endpoint '{endpointName}' has no username.");
        if (string.IsNullOrWhiteSpace(endpoint.Password))
            throw new ConfigurationValidationException($"Endpoint '{endpointName}' has no password.");
    }
}

public sealed class ConfigurationValidationException(string message) : InvalidOperationException(message);