namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class BaselineDataReader : IBaselineDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	private const string PassingBaselinesSql = """
		WITH PassingBaselines AS
		(
			SELECT
				response.Scenario_id AS ScenarioId,
				request.Quote_ref AS QuoteRef,
				request.Product_code AS ProductCode,
				request.Schem_code AS SchemeCode,
				response.XML_Response AS XmlResponse,
				response.Status AS Status,
				response.Last_updated AS LastUpdated,
				ROW_NUMBER() OVER
				(
					PARTITION BY request.Product_code, request.Schem_code
					ORDER BY response.Last_updated DESC, response.Scenario_id DESC
				) AS RowNumber
			FROM xml_response AS response
			INNER JOIN xml_request AS request ON request.Scenario_id = response.Scenario_id
			WHERE response.Status = 'PASS'
		)
		SELECT ScenarioId, QuoteRef, ProductCode, SchemeCode, XmlResponse, Status, LastUpdated
		FROM PassingBaselines
		WHERE RowNumber = 1;
		""";

	private const string PassingBaselineByIdSql = """
		SELECT
			response.Scenario_id AS ScenarioId,
			request.Quote_ref AS QuoteRef,
			response.XML_Response AS XmlResponse,
			response.Build_id AS BuildId,
			response.Create_date AS CreatedDate,
			response.Last_updated AS LastUpdated,
			response.Status AS Status
		FROM xml_response AS response
		INNER JOIN xml_request AS request ON request.Scenario_id = response.Scenario_id
		WHERE response.Scenario_id = @ScenarioId
		  AND response.Status = 'PASS';
		""";

	public BaselineDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(PassingBaselinesSql, cancellationToken: cancellationToken);
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
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(PassingBaselineByIdSql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
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
