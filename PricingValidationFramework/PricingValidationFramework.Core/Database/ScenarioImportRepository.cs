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

	private const string ReadUpdateIdentitiesSql = """
		SELECT Scenario_id AS ScenarioId, Product_code AS ProductCode, Scheme_code AS SchemeCode
		FROM xml_request WITH (UPDLOCK, HOLDLOCK)
		WHERE Scenario_id IN @ScenarioIds;
		""";

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

	public async Task<ScenarioImportBatchResult> ApplyBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> inserts,
		IReadOnlyCollection<ScenarioRequestImport> updates,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(inserts);
		ArgumentNullException.ThrowIfNull(updates);
		if (inserts.Count + updates.Count > 1000)
		{
			throw new ArgumentOutOfRangeException(nameof(inserts), "A write batch cannot exceed 1000 rows.");
		}
		if (inserts.Count + updates.Count == 0)
		{
			return new ScenarioImportBatchResult(0, 0);
		}
		if (inserts.Concat(updates).Select(row => row.ScenarioId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inserts.Count + updates.Count)
		{
			throw new ArgumentException("A write batch must contain unique Scenario_id values.");
		}

		await using var connection = await connectionFactory.OpenAsync(cancellationToken);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

		await connection.ExecuteAsync(new CommandDefinition(
			"SET XACT_ABORT ON;", transaction: transaction, cancellationToken: cancellationToken));

		if (updates.Count > 0)
		{
			var identities = await connection.QueryAsync<(string ScenarioId, string ProductCode, string SchemeCode)>(new CommandDefinition(
				ReadUpdateIdentitiesSql,
				new { ScenarioIds = updates.Select(row => row.ScenarioId).ToArray() },
				transaction, commandTimeout: 120, cancellationToken: cancellationToken));
			ValidateUpdateIdentities(updates, identities);
		}

		foreach (var chunk in updates.Chunk(MaxRowsPerStatement))
		{
			var updated = await connection.ExecuteAsync(new CommandDefinition(
				BuildUpdateSql(chunk.Length), BuildParameters(chunk), transaction,
				commandTimeout: 120, cancellationToken: cancellationToken));
			if (updated != chunk.Length)
			{
				throw new InvalidOperationException("An imported request is missing or its product or scheme changed.");
			}
		}

		foreach (var chunk in inserts.Chunk(MaxRowsPerStatement))
		{
			var inserted = await connection.ExecuteAsync(new CommandDefinition(
				BuildInsertSql(chunk.Length), BuildParameters(chunk), transaction,
				commandTimeout: 120, cancellationToken: cancellationToken));

			if (inserted != chunk.Length)
			{
				throw new InvalidOperationException("An imported request Scenario_id already exists.");
			}
		}

		await transaction.CommitAsync(cancellationToken);
		return new ScenarioImportBatchResult(inserts.Count, updates.Count);
	}

	internal static void ValidateUpdateIdentities(
		IReadOnlyCollection<ScenarioRequestImport> updates,
		IEnumerable<(string ScenarioId, string ProductCode, string SchemeCode)> identities)
	{
		var existing = identities.ToDictionary(row => row.ScenarioId.Trim(), StringComparer.OrdinalIgnoreCase);
		foreach (var update in updates)
		{
			if (!existing.TryGetValue(update.ScenarioId, out var stored) ||
				!string.Equals(stored.ProductCode?.Trim(), update.ProductCode, StringComparison.Ordinal) ||
				!string.Equals(stored.SchemeCode?.Trim(), update.SchemeCode, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("An imported request is missing or its product or scheme changed.");
			}
		}
	}

	internal static DynamicParameters BuildParameters(IReadOnlyList<ScenarioRequestImport> requests)
	{
		var parameters = new DynamicParameters();
		for (var row = 0; row < requests.Count; row++)
		{
			var request = requests[row];
			parameters.Add($"s{row}", request.ScenarioId);
			parameters.Add($"q{row}", request.QuoteRef);
			parameters.Add($"p{row}", request.ProductCode);
			parameters.Add($"c{row}", request.SchemeCode);
			parameters.Add($"x{row}", request.XmlRequest, DbType.AnsiString, size: -1);
			parameters.Add($"t{row}", request.TestTags);
		}
		return parameters;
	}

	internal static string BuildUpdateSql(int rowCount)
	{
		return $"""
			UPDATE target
			SET XML_request = v.XML_request,
			    Quote_ref = v.Quote_ref
			FROM xml_request AS target WITH (UPDLOCK, HOLDLOCK)
			INNER JOIN (VALUES
				{BuildValuesRows(rowCount)}) AS v ({InsertColumns})
			    ON target.Scenario_id = v.Scenario_id
			WHERE target.Product_code = v.Product_code
			  AND target.Scheme_code = v.Scheme_code;
			""";
	}

	internal static string BuildInsertSql(int rowCount)
	{
		return $"""
			INSERT INTO xml_request ({InsertColumns}, Create_date)
			SELECT {InsertColumns}, SYSUTCDATETIME()
			FROM (VALUES
				{BuildValuesRows(rowCount)}) AS v ({InsertColumns})
			WHERE NOT EXISTS (
			    SELECT 1 FROM xml_request AS target WITH (UPDLOCK, HOLDLOCK)
			    WHERE target.Scenario_id = v.Scenario_id);
			""";
	}

	private static string BuildValuesRows(int rowCount)
	{
		if (rowCount is < 1 or > MaxRowsPerStatement)
		{
			throw new ArgumentOutOfRangeException(nameof(rowCount), $"A statement holds 1 to {MaxRowsPerStatement} rows.");
		}

		return string.Join(",\n\t", Enumerable.Range(0, rowCount)
			.Select(row => $"(@s{row}, @q{row}, @p{row}, @c{row}, @x{row}, @t{row})"));
	}
}
