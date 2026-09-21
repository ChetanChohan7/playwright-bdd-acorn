using System.Diagnostics;
using System.Net;
using FuzzyPricingMatcher.Tests.Configuration;
using NLog;
using RestSharp;

namespace FuzzyPricingMatcher.Tests.ExternalAPIAccess;

public sealed class ExternalXmlServiceClient : IExternalXmlServiceClient
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly ExternalServiceClientFactory clientFactory;
    private readonly ExternalAPIRequestUrlBuilder resourceBuilder;
    private readonly ExternalAPIRetryPolicy retryPipeline;
    private readonly IRestRequestExecutor executor;
    private readonly int timeoutSeconds;

    public ExternalXmlServiceClient(ExternalServiceClientFactory clientFactory, ExternalAPIRequestUrlBuilder resourceBuilder, ExternalAPIRetryPolicy retryPipeline, IRestRequestExecutor? executor = null, int timeoutSeconds = 60)
    {
        if (timeoutSeconds <= 0)
            throw new ExternalAPIConfigurationException("API timeout must be greater than zero.");
        this.clientFactory = clientFactory;
        this.resourceBuilder = resourceBuilder;
        this.retryPipeline = retryPipeline;
        this.executor = executor ?? new RestRequestExecutor();
        this.timeoutSeconds = timeoutSeconds;
    }

    public Task<ExternalAPIResponse> SendXmlRequestAsync(ExternalXmlRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var resource = resourceBuilder.BuildRequestUri(request.Endpoint, request.ApiDate);
        var client = clientFactory.GetClient(request.Route.EndpointName, request.Endpoint);
        return retryPipeline.ExecuteAsync($"API:{request.ScenarioId}", async (attempt, token) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var restRequest = new RestRequest(resource.ResourcePath, Method.Post)
                .AddHeader("Content-Type", string.IsNullOrWhiteSpace(request.Endpoint.ContentType) ? "application/xml" : request.Endpoint.ContentType)
                .AddStringBody(request.RawXml, string.IsNullOrWhiteSpace(request.Endpoint.ContentType) ? "application/xml" : request.Endpoint.ContentType);
            if (resource.IsQueryDate)
                restRequest.AddQueryParameter(resource.DateParameterName!, resource.DateValue!);
            RestResponse response;
            try { response = await executor.ExecuteAsync(client, restRequest, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception) { throw new ExternalAPITransportException("API transport failed.", exception); }
            stopwatch.Stop();
            var statusCode = response.StatusCode == 0 ? null : (int?)response.StatusCode;
            var retryAfter = ReadRetryAfter(response);
            Logger.Info("API attempt BuildId={BuildId} ScenarioId={ScenarioId} SchemeCode={SchemeCode} EndpointName={EndpointName} Resource={Resource} Attempt={Attempt} StatusCode={StatusCode} RequestBytes={RequestBytes} ResponseBytes={ResponseBytes} DurationMs={DurationMs}", request.BuildId, request.ScenarioId, request.SchemeCode, request.Route.EndpointName, resource.ResourcePath, attempt, statusCode, System.Text.Encoding.UTF8.GetByteCount(request.RawXml), System.Text.Encoding.UTF8.GetByteCount(response.Content ?? string.Empty), stopwatch.ElapsedMilliseconds);
            if (statusCode is >= 200 and <= 299)
                return ExternalAPIResponse.Success(statusCode.Value, response.Content ?? string.Empty, attempt, stopwatch.Elapsed);
            return ExternalAPIResponse.Failure(statusCode, response.ErrorMessage ?? $"API returned HTTP {statusCode?.ToString() ?? "no status"}.", attempt, stopwatch.Elapsed, retryAfter);
        }, cancellationToken);
    }

    private static void ValidateRequest(ExternalXmlRequest request)
    {
        if (!request.Route.Enabled)
            throw new ExternalAPIConfigurationException($"Route for SchemeCode '{request.SchemeCode}' is disabled.");
        ExternalServiceClientFactory.ValidateEndpoint(request.Route.EndpointName, request.Endpoint);
        if (string.IsNullOrWhiteSpace(request.RawXml))
            throw new ExternalAPIRequestException("Raw XML request is required.");
    }

    private static TimeSpan? ReadRetryAfter(RestResponse response)
    {
        var value = response.Headers?.FirstOrDefault(header => string.Equals(header.Name?.ToString(), "Retry-After", StringComparison.OrdinalIgnoreCase))?.Value?.ToString();
        return int.TryParse(value, out var seconds) && seconds >= 0 ? TimeSpan.FromSeconds(seconds) : null;
    }
}