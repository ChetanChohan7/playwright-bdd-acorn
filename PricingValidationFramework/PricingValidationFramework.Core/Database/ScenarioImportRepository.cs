namespace PricingValidationFramework.Core.Database;

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Models.Database;

public sealed class ScenarioImportRepository(SqlConnectionFactory connectionFactory) : IScenarioImportRepository
{
	private const string ReadExistingSql = """
		SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef,
		       Product_code AS ProductCode, Scheme_code AS SchemeCode,
		       XML_request AS XmlRequest, Test_tags AS TestTags
		FROM xml_request WHERE Scenario_id IN @ScenarioIds;
		""";

	// Rows go to SQL Server as parameters in multi-row VALUES statements, so the loader needs
	// only SELECT and INSERT on xml_request: no temporary tables, table types or bulk copy.
	// Six parameters per row; 2,000 of SQL Server's 2,100-per-command limit gives 333 rows.
	internal const int ParametersPerRow = 6;
	internal const int MaxRowsPerStatement = 2000 / ParametersPerRow;

	private const string InsertColumns = "Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags";

	public async Task<ScenarioImportSnapshot> ReadExistingAsync(
		IReadOnlyCollection<string> scenarioIds,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenarioIds);
		if (scenarioIds.Count > 1000)
		{
			throw new ArgumentOutOfRangeException(nameof(scenarioIds), "A lookup batch cannot exceed 1000 scenario IDs.");
		}
		if (scenarioIds.Count == 0)
		{
			return new ScenarioImportSnapshot([]);
		}

		await using var connection = await connectionFactory.OpenAsync(cancellationToken);
		var requests = (await connection.QueryAsync<ScenarioRequestImport>(new CommandDefinition(
			ReadExistingSql, new { ScenarioIds = scenarioIds }, cancellationToken: cancellationToken))).AsList();
		return new ScenarioImportSnapshot(requests);
	}

	public async Task InsertBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> requests,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(requests);
		if (requests.Count > 1000)
		{
			throw new ArgumentOutOfRangeException(nameof(requests), "An insert batch cannot exceed 1000 rows.");
		}
		if (requests.Count == 0)
		{
			return;
		}

		await using var connection = await connectionFactory.OpenAsync(cancellationToken);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		// Any error rolls back the whole batch on the server, including a client-side timeout.
		await connection.ExecuteAsync(new CommandDefinition(
			"SET XACT_ABORT ON;", transaction: transaction, cancellationToken: cancellationToken));

		foreach (var chunk in requests.Chunk(MaxRowsPerStatement))
		{
			var parameters = new DynamicParameters();
			for (var row = 0; row < chunk.Length; row++)
			{
				var request = chunk[row];
				parameters.Add($"s{row}", request.ScenarioId);
				parameters.Add($"q{row}", request.QuoteRef);
				parameters.Add($"p{row}", request.ProductCode);
				parameters.Add($"c{row}", request.SchemeCode);
				parameters.Add($"x{row}", request.XmlRequest);
				parameters.Add($"t{row}", request.TestTags);
			}

			var inserted = await connection.ExecuteAsync(new CommandDefinition(
				BuildInsertSql(chunk.Length), parameters, transaction, commandTimeout: 120, cancellationToken: cancellationToken));

			// The NOT EXISTS guard skips any Scenario_id that appeared since preflight. Fewer rows than
			// sent means one already exists: throwing here rolls back every chunk in this batch.
			if (inserted != chunk.Length)
			{
				throw new InvalidOperationException("An imported request Scenario_id already exists.");
			}
		}

		await transaction.CommitAsync(cancellationToken);
	}

	internal static string BuildInsertSql(int rowCount)
	{
		if (rowCount is < 1 or > MaxRowsPerStatement)
		{
			throw new ArgumentOutOfRangeException(nameof(rowCount), $"A statement holds 1 to {MaxRowsPerStatement} rows.");
		}

		var rows = string.Join(",\n\t", Enumerable.Range(0, rowCount)
			.Select(row => $"(@s{row}, @q{row}, @p{row}, @c{row}, @x{row}, @t{row})"));
		return $"""
			INSERT INTO xml_request ({InsertColumns}, Create_date)
			SELECT {InsertColumns}, SYSUTCDATETIME()
			FROM (VALUES
				{rows}) AS v ({InsertColumns})
			WHERE NOT EXISTS (
			    SELECT 1 FROM xml_request AS target WITH (UPDLOCK, HOLDLOCK)
			    WHERE target.Scenario_id = v.Scenario_id);
			""";
	}
}
