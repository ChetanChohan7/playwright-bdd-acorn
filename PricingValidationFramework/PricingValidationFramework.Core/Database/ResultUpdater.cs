namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class ResultUpdater
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	// PASS: the Radar response becomes the new baseline.
	private const string UpdatePassingResultSql = """
		UPDATE xml_response
		SET Status = @Status,
		    Last_updated = SYSUTCDATETIME(),
		    XML_Response = @XmlResponse
		WHERE Scenario_id = @ScenarioId;
		""";

	// FAIL: only the status changes, so the existing baseline XML is never overwritten.
	private const string UpdateFailedStatusSql = """
		UPDATE xml_response
		SET Status = @Status
		WHERE Scenario_id = @ScenarioId;
		""";

	// New scenario: the first Radar response becomes its baseline. The NOT EXISTS guard (with
	// UPDLOCK/HOLDLOCK) means an existing baseline is never replaced, even by a concurrent run.
	private const string InsertBaselineSql = """
		INSERT INTO xml_response (Scenario_id, XML_Response, Status, Create_date, Last_updated)
		SELECT @ScenarioId, @XmlResponse, @Status, SYSUTCDATETIME(), SYSUTCDATETIME()
		WHERE NOT EXISTS (
		    SELECT 1 FROM xml_response WITH (UPDLOCK, HOLDLOCK)
		    WHERE Scenario_id = @ScenarioId);
		""";

	public ResultUpdater(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task UpdateResultAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		var affectedRows = string.Equals(response.Status, "PASS", StringComparison.Ordinal)
			? await ExecuteAsync(UpdatePassingResultSql, new
			{
				response.Status,
				response.XmlResponse,
				response.ScenarioId
			}, cancellationToken)
			: await ExecuteAsync(UpdateFailedStatusSql, new
			{
				response.Status,
				response.ScenarioId
			}, cancellationToken);
		EnsureSingleRowUpdated(response.ScenarioId, affectedRows);
	}

	public async Task InsertBaselineAsync(ScenarioResponse response, CancellationToken cancellationToken = default)
	{
		var affectedRows = await ExecuteAsync(InsertBaselineSql, new
		{
			response.ScenarioId,
			response.XmlResponse,
			response.Status
		}, cancellationToken);
		if (affectedRows != 1)
		{
			throw new InvalidOperationException(
				$"A baseline already exists in xml_response for ScenarioId '{response.ScenarioId}'; it was not replaced.");
		}
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