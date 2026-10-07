using RestSharp;

namespace ClientAutomationFramework.Core.ExternalAPIAccess;

/// GET-only client returning a JSON body. Takes the same shared RestClient as XmlApiClient -
/// the process creates exactly one RestClient instance for every scheme it calls.
public sealed class JsonApiClient(RestClient client, SchemeConfig scheme)
{
    public async Task<ApiCallResult> GetAsync(string quoteRef, CancellationToken cancellationToken = default)
    {
        var request = new RestRequest(EndpointResolver.Resolve(scheme, quoteRef), Method.Get)
            .AddHeader("Accept", "application/json");
        try
        {
            var response = await client.ExecuteAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return new ApiCallResult(true, response.Content ?? string.Empty, string.Empty);
            var statusCode = response.StatusCode == 0 ? "no status" : ((int)response.StatusCode).ToString();
            return new ApiCallResult(false, response.Content ?? string.Empty, response.ErrorMessage ?? $"GET returned HTTP {statusCode}.");
        }
        catch (Exception exception)
        {
            return new ApiCallResult(false, string.Empty, exception.Message);
        }
    }
}
