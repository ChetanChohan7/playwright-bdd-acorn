namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class BaselineDataReader : IBaselineDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	public BaselineDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
	{
		const string sql = """
			WITH PassingBaselines AS
			(
				SELECT
					Scenario_id AS ScenarioId,
					Quote_ref AS QuoteRef,
					Product_code AS ProductCode,
					Scheme_code AS SchemeCode,
					XML_response AS XmlResponse,
					Status AS Status,
					Last_updated AS LastUpdated,
					ROW_NUMBER() OVER
					(
						PARTITION BY Product_code, Scheme_code
						ORDER BY LastUpdated DESC, Scenario_id DESC
					) AS RowNumber
				FROM xml_response
				WHERE Status = 'PASS'
			)
			SELECT ScenarioId, QuoteRef, ProductCode, SchemeCode, XmlResponse, Status, LastUpdated
			FROM PassingBaselines
			WHERE RowNumber = 1;
			""";

		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
				var scenarios = await connection.QueryAsync<IceBaselineScenario>(command);
				return scenarios.AsList();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	public async Task<ScenarioResponse> GetPassingBaselineByScenarioIdAsync(
		string scenarioId,
		CancellationToken cancellationToken = default)
	{
		const string sql = """
			SELECT
				Scenario_id AS ScenarioId,
				Quote_ref AS QuoteRef,
				Product_code AS ProductCode,
				Scheme_code AS SchemeCode,
				XML_response AS XmlResponse,
				Build_id AS BuildId,
				Created_date AS CreatedDate,
				Last_updated AS LastUpdated,
				Status AS Status
			FROM xml_response
			WHERE Scenario_id = @ScenarioId
			  AND Status = 'PASS';
			""";

		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
				var baselines = (await connection.QueryAsync<ScenarioResponse>(command)).AsList();

				return baselines.Count switch
				{
					1 => baselines[0],
					0 => throw new InvalidDataException($"No passing baseline was found for ScenarioId '{scenarioId}'."),
					_ => throw new InvalidDataException($"Multiple passing baselines were found for ScenarioId '{scenarioId}'.")
				};
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}
