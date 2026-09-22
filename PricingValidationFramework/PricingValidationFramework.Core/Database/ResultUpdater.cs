namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class ResultUpdater
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	public ResultUpdater(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
	}

	public Task UpdatePassResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		const string sql = """
			UPDATE TB_RESPONSE
			SET Status = @Status,
			    Build_id = @BuildId,
			    LastUpdated = SYSUTCDATETIME(),
			    Xml_response = @XmlResponse
			WHERE Scenario_id = @ScenarioId;
			""";

		return ExecuteAsync(sql, new
		{
			response.Status,
			response.BuildId,
			response.XmlResponse,
			response.ScenarioId
		}, cancellationToken);
	}

	public Task UpdateFailResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		const string sql = """
			UPDATE TB_RESPONSE
			SET Status = @Status,
			    Build_id = @BuildId,
			    LastUpdated = SYSUTCDATETIME()
			WHERE Scenario_id = @ScenarioId;
			""";

		return ExecuteAsync(sql, new
		{
			response.Status,
			response.BuildId,
			response.ScenarioId
		}, cancellationToken);
	}

	private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				await connection.ExecuteAsync(command);
				return;
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}
