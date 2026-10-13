namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class BaselineDataReader : IBaselineDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	private const string PassingBaselinesSelect = """
		WITH PassingBaselines AS
		(
			SELECT
				response.Scenario_id AS ScenarioId,
				request.Quote_ref AS QuoteRef,
				request.Product_code AS ProductCode,
				request.Scheme_code AS SchemeCode,
				response.XML_Response AS XmlResponse,
				response.Status AS Status,
				response.Last_updated AS LastUpdated,
				ROW_NUMBER() OVER
				(
					PARTITION BY request.Product_code, request.Scheme_code
					ORDER BY response.Last_updated DESC, response.Scenario_id DESC
				) AS RowNumber
			FROM [Xml].[Response] AS response
			INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id
			WHERE response.Status = 'PASS'
		""";
	private const string PassingBaselinesResult = """
		)
		SELECT ScenarioId, QuoteRef, ProductCode, SchemeCode, XmlResponse, Status, LastUpdated
		FROM PassingBaselines
		WHERE RowNumber = 1;
		""";
	private const string PassingBaselinesSql = PassingBaselinesSelect + "\n" + PassingBaselinesResult;

	// The same selection limited to one product, matching Radar's TEST_TAG filter
	// (RequestDataReader.ProductScenariosSql), so ICE checks the products Radar just ran.
	private const string ProductPassingBaselinesSql =
		PassingBaselinesSelect + "\n\t\t\tAND request.Product_code = @ProductCode\n" + PassingBaselinesResult;

	// No Status filter: Radar reports "no baseline yet" (MissingBaseline: run DataLoader seed)
	// and "baseline exists but isn't PASS" as different errors.
	private const string BaselineByIdSql = """
		SELECT
			response.Scenario_id AS ScenarioId,
			request.Quote_ref AS QuoteRef,
			response.XML_Response AS XmlResponse,
			response.row_creationdate AS CreatedDate,
			response.Last_updated AS LastUpdated,
			response.Status AS Status
		FROM [Xml].[Response] AS response
		INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id
		WHERE response.Scenario_id = @ScenarioId;
		""";

	public BaselineDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
	{
		return QueryPassingBaselinesAsync(PassingBaselinesSql, null, cancellationToken);
	}

	public Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosByProductCodeAsync(
		string productCode,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
		return QueryPassingBaselinesAsync(ProductPassingBaselinesSql, new { ProductCode = productCode }, cancellationToken);
	}

	private async Task<IReadOnlyList<IceBaselineScenario>> QueryPassingBaselinesAsync(
		string sql,
		object? parameters,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
				var scenarios = await connection.QueryAsync<IceBaselineScenario>(command);
				return scenarios.AsList();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	public async Task<ScenarioResponse?> GetBaselineByScenarioIdAsync(
		string scenarioId,
		CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(BaselineByIdSql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
				var baselines = (await connection.QueryAsync<ScenarioResponse>(command)).AsList();

				return baselines.Count switch
				{
					1 => baselines[0],
					0 => null,
					_ => throw new InvalidDataException($"Multiple baselines were found for ScenarioId '{scenarioId}'.")
				};
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}
