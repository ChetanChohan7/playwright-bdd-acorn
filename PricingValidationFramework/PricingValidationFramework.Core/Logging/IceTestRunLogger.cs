namespace PricingValidationFramework.Core.Logging;

using Microsoft.Extensions.Logging;

public class IceTestRunLogger
{
	private readonly ILogger<IceTestRunLogger> logger;

	public IceTestRunLogger(ILogger<IceTestRunLogger> logger)
	{
		this.logger = logger;
	}

	public void DatabaseActivity(string message, params object[] arguments) => logger.LogInformation(message, arguments);

	public void ApiActivity(string message, params object[] arguments) => logger.LogInformation(message, arguments);

	public void ResponseSummary(string message, params object[] arguments) => logger.LogInformation(message, arguments);

	public void AssertionOutcome(string message, params object[] arguments) => logger.LogInformation(message, arguments);

	public void Cancellation(string message, params object[] arguments) => logger.LogWarning(message, arguments);

	public void Error(Exception exception, string message, params object[] arguments) => logger.LogError(exception, message, arguments);
}
