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

	public void ExecutionCompleted(string buildId) => logger.LogInformation("ICE workload completed. BuildId={BuildId}.", buildId);

	public void ExecutionFailed(string scenarioId, string quoteRef, Exception exception)
		=> logger.LogError(exception, "ICE execution failed. ScenarioId={ScenarioId}, QuoteRef={QuoteRef}.", scenarioId, quoteRef);

	public void Cancellation(string message, params object[] arguments) => logger.LogWarning(message, arguments);
}
