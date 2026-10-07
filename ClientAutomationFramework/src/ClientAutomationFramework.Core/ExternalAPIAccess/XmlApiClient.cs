using RestSharp;

namespace ClientAutomationFramework.Core.ExternalAPIAccess;

public sealed record ApiCallResult(bool Successful, string Body, string Error);

/// Sends raw request XML and returns the raw response XML. Takes the shared RestClient rather
/// than owning one, so every client in this framework reuses a single instance.
public sealed class XmlApiClient(RestClient client, SchemeConfig scheme)
{
    public async Task<ApiCallResult> SendAsync(string rawRequestXml, CancellationToken cancellationToken = default)
    {
        var contentType = string.IsNullOrWhiteSpace(scheme.ContentType) ? "application/xml" : scheme.ContentType;
        var request = new RestRequest(EndpointResolver.Resolve(scheme), Method.Post)
            .AddHeader("Content-Type", contentType)
            .AddStringBody(rawRequestXml, contentType);
        try
        {
            var response = await client.ExecuteAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return new ApiCallResult(true, response.Content ?? string.Empty, string.Empty);
            var statusCode = response.StatusCode == 0 ? "no status" : ((int)response.StatusCode).ToString();
            return new ApiCallResult(false, response.Content ?? string.Empty, response.ErrorMessage ?? $"API returned HTTP {statusCode}.");
        }
        catch (Exception exception)
        {
            return new ApiCallResult(false, string.Empty, exception.Message);
        }
    }
}
