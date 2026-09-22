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
	}

	public async Task<IReadOnlyList<IceBaselineScenario>> GetPassingBaselineScenariosAsync(CancellationToken cancellationToken = default)
	{
		const string sql = """
			WITH PassingBaselines AS
			(
				SELECT
					Scenario_id,
					Quote_ref,
					Product_code,
					Scheme_code,
					Xml_response,
					Status,
					LastUpdated,
					ROW_NUMBER() OVER
					(
						PARTITION BY Product_code, Scheme_code
						ORDER BY LastUpdated DESC, Scenario_id DESC
					) AS RowNumber
				FROM TB_RESPONSE
				WHERE Status = 'PASS'
			)
			SELECT Scenario_id, Quote_ref, Product_code, Scheme_code, Xml_response, Status, LastUpdated
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
}
