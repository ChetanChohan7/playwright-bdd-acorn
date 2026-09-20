using System.Collections.Concurrent;
using FuzzyPricingMatcher.Tests.Configuration;
using RestSharp;
using RestSharp.Authenticators;

namespace FuzzyPricingMatcher.Tests.Api;

public sealed class RestClientFactory : IRestClientFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<RestClient>> clients = new(StringComparer.OrdinalIgnoreCase);

    public RestClient GetClient(string endpointName, EndpointSettings endpoint)
    {
        ValidateEndpoint(endpointName, endpoint);
        return clients.GetOrAdd(endpointName, _ => new Lazy<RestClient>(() =>
        {
            var options = new RestClientOptions(endpoint.BaseUrl)
            {
                Authenticator = new HttpBasicAuthenticator(endpoint.Username, endpoint.Password)
            };
            return new RestClient(options);
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public void Dispose()
    {
        foreach (var client in clients.Values.Where(value => value.IsValueCreated).Select(value => value.Value))
            client.Dispose();
        clients.Clear();
    }

    internal static void ValidateEndpoint(string endpointName, EndpointSettings endpoint)
    {
        if (!endpoint.Enabled)
            throw new ApiConfigurationException($"Endpoint '{endpointName}' is disabled.");
        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var uri) || uri.Host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            throw new ApiConfigurationException($"Endpoint '{endpointName}' does not have a permitted live host.");
        if (string.IsNullOrWhiteSpace(endpoint.Resource))
            throw new ApiConfigurationException($"Endpoint '{endpointName}' has no resource.");
        if (string.IsNullOrWhiteSpace(endpoint.Username) || string.IsNullOrWhiteSpace(endpoint.Password))
            throw new ApiConfigurationException($"Endpoint '{endpointName}' requires username and password.");
    }
}