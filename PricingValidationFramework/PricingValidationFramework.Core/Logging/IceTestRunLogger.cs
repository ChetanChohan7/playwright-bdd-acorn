namespace PricingValidationFramework.Core.Logging;

using Microsoft.Extensions.Logging;

public class IceTestRunLogger
{
	private readonly ILogger<IceTestRunLogger> logger;

	public IceTestRunLogger(ILogger<IceTestRunLogger> logger)
	{
		this.logger = logger;
	}

	public void ExecutionStarted(string buildId) => logger.LogInformation("ICE workload started. BuildId={BuildId}.", buildId);

	public void ScenarioStarted(string scenarioId, string quoteRef) => logger.LogInformation("ICE scenario started. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.", scenarioId, quoteRef);

	public void RequestPrepared(string scenarioId, string quoteRef, string requestUrl) => logger.LogInformation("ICE request URL. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}, RequestUrl={RequestUrl}.", scenarioId, quoteRef, requestUrl);

	public void ValidationResult(string scenarioId, decimal iceValue, decimal baselineValue, bool passed)
		=> logger.LogInformation("ICE validation completed. ScenarioId={ScenarioId}, IceValue={IceValue}, BaselineValue={BaselineValue}, Passed={Passed}.", scenarioId, iceValue, baselineValue, passed);

	public void ReportGenerated(string buildId) => logger.LogInformation("ICE report generation completed. BuildId={BuildId}.", buildId);

	public void ExecutionCompleted(string buildId) => logger.LogInformation("ICE workload completed. BuildId={BuildId}.", buildId);

	public void ExecutionFailed(string scenarioId, string quoteRef, Exception exception)
		=> logger.LogError(exception, "ICE execution failed. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.", scenarioId, quoteRef);

	public void Cancellation(string message, params object[] arguments) => logger.LogWarning(message, arguments);
}
