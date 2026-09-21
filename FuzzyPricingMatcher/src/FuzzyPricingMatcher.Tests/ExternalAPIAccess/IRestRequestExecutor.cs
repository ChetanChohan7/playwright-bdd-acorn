using RestSharp;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public interface IRestRequestExecutor
{
    Task<RestResponse> ExecuteAsync(RestClient client, RestRequest request, CancellationToken cancellationToken);
}

public sealed class RestRequestExecutor : IRestRequestExecutor
{
    public Task<RestResponse> ExecuteAsync(RestClient client, RestRequest request, CancellationToken cancellationToken) => client.ExecuteAsync(request, cancellationToken);
}