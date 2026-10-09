namespace PricingValidationFramework.Core.Database;

using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Models.Database;

public class RequestDataReader
{
	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	private const string ScenarioColumns = """
		Scenario_id AS ScenarioId,
		Quote_ref AS QuoteRef,
		Scheme_code AS SchemeCode,
		Product_code AS ProductCode,
		XML_request AS XmlRequest,
		Test_tags AS TestTags,
		Create_date AS CreatedDate
		""";
	private const string AllScenariosSql = $"SELECT {ScenarioColumns} FROM xml_request ORDER BY Create_date, Scenario_id;";
	private const string ProductScenariosSql = $"SELECT {ScenarioColumns} FROM xml_request WHERE Product_code = @ProductCode ORDER BY Create_date, Scenario_id;";
	private const string ScenarioByIdSql = $"SELECT {ScenarioColumns} FROM xml_request WHERE Scenario_id = @ScenarioId;";

	public RequestDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetAllScenariosAsync(CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(AllScenariosSql, null, cancellationToken);
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetScenariosByProductCodeAsync(string productCode, CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync(ProductScenariosSql, new { ProductCode = productCode }, cancellationToken);
	}

	public async Task<ScenarioRequest?> GetScenarioByIdAsync(string scenarioId, CancellationToken cancellationToken = default)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(ScenarioByIdSql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
				return await connection.QuerySingleOrDefaultAsync<ScenarioRequest>(command);
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}

	private async Task<IReadOnlyList<ScenarioRequest>> QueryScenariosAsync(
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
				var scenarios = await connection.QueryAsync<ScenarioRequest>(command);
				return scenarios.AsList();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}