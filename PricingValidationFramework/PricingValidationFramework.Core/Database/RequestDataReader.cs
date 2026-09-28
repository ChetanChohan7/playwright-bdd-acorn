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
		Created_date AS CreatedDate
		""";

	public RequestDataReader(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory;
		this.retrySettings = retrySettings ?? new RetrySettings();
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetAllScenariosAsync(CancellationToken cancellationToken = default)
	{
		return QueryScenariosAsync($"SELECT {ScenarioColumns} FROM xml_request ORDER BY Created_date, Scenario_id;", null, cancellationToken);
	}

	public Task<IReadOnlyList<ScenarioRequest>> GetScenariosByTestTagAsync(string testTag, CancellationToken cancellationToken = default)
	{
		const string sql = $"SELECT {ScenarioColumns} FROM xml_request WHERE Test_tags = @TestTag ORDER BY Created_date, Scenario_id;";
		return QueryScenariosAsync(sql, new { TestTag = testTag }, cancellationToken);
	}

// this is not used in the code remove it 
	public async Task<ScenarioRequest?> GetScenarioByIdAsync(string scenarioId, CancellationToken cancellationToken = default)
	{
		const string sql = $"SELECT {ScenarioColumns} FROM xml_request WHERE Scenario_id = @ScenarioId;";

		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = connectionFactory.Create();
				await connection.OpenAsync(cancellationToken);
				var command = new CommandDefinition(sql, new { ScenarioId = scenarioId }, cancellationToken: cancellationToken);
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
// rename remove unused methods