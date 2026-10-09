namespace PricingValidationFramework.Core.Logging;

using Microsoft.Extensions.Logging;

public class RadarTestRunLogger
{
	private readonly ILogger<RadarTestRunLogger> logger;

	public RadarTestRunLogger(ILogger<RadarTestRunLogger> logger)
	{
		this.logger = logger;
	}

	public void ExecutionStarted(string buildId) => logger.LogInformation("Radar workload started. BuildId={BuildId}.", buildId);

	public void ExecutionCompleted(string buildId) => logger.LogInformation("Radar workload completed. BuildId={BuildId}.", buildId);

	public void ExecutionFailed(
		string scenarioId,
		string quoteRef,
		string productCode,
		string schemeCode,
		string? endpointName,
		string executionStage,
		Exception exception,
		int? statusCode = null,
		string? safeDetail = null)
		=> logger.LogError(
			"Radar execution failed. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, ProductCode={ProductCode}, SchemeCode={SchemeCode}, EndpointName={EndpointName}, ExecutionStage={ExecutionStage}, ErrorType={ErrorType}, StatusCode={StatusCode}, ErrorDetail={ErrorDetail}.",
			scenarioId,
			quoteRef,
			productCode,
			schemeCode,
			endpointName ?? "unresolved",
			executionStage,
			exception.GetType().Name,
			statusCode,
			safeDetail);

	public void ReportGenerationFailed(Exception exception)
		=> logger.LogError(
			"Radar report generation failed. ErrorType={ErrorType}.",
			exception.GetType().Name);

	public void Cancellation(string message, params object[] arguments) => logger.LogWarning(message, arguments);
}
