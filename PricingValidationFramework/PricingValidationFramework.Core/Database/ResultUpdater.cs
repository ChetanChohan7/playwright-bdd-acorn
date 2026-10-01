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
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task UpdatePassResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		const string sql = """
			UPDATE xml_response
			SET Status = @Status,
			    Build_id = @BuildId,
			    Last_updated = SYSUTCDATETIME(),
			    XML_response = @XmlResponse
			WHERE Scenario_id = @ScenarioId;
			""";

		var affectedRows = await ExecuteAsync(sql, new
		{
			response.Status,
			response.BuildId,
			response.XmlResponse,
			response.ScenarioId
		}, cancellationToken);
		EnsureSingleRowUpdated(response.ScenarioId, affectedRows);
	}

	public async Task UpdateFailResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		const string sql = """
			UPDATE xml_response
			SET Status = @Status,
			    Build_id = @BuildId,
			    Last_updated = SYSUTCDATETIME()
			WHERE Scenario_id = @ScenarioId;
			""";

		var affectedRows = await ExecuteAsync(sql, new
		{
			response.Status,
			response.BuildId,
			response.ScenarioId
		}, cancellationToken);
		EnsureSingleRowUpdated(response.ScenarioId, affectedRows);
	}

	private async Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				return await connection.ExecuteAsync(command);
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	private static void EnsureSingleRowUpdated(string scenarioId, int affectedRows)
	{
		if (affectedRows != 1)
		{
			throw new InvalidOperationException(
				$"Expected exactly one xml_response row to be updated for ScenarioId '{scenarioId}', but {affectedRows} rows were updated.");
		}
	}
} 