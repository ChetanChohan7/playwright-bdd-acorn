namespace PricingValidationFramework.Core.Database;

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Models.Database;

public sealed class ScenarioImportRepository(SqlConnectionFactory connectionFactory) : IScenarioImportRepository
{
	private const string ReadExistingSql = """
		SELECT Scenario_id AS ScenarioId, Quote_ref AS QuoteRef,
		       Product_code AS ProductCode, Schem_code AS SchemeCode,
		       XML_request AS XmlRequest, Test_tags AS TestTags
		FROM xml_request WHERE Scenario_id IN @ScenarioIds;
		SELECT Scenario_id AS ScenarioId, XML_Response AS XmlResponse,
		       Build_id AS BuildId, Status AS Status
		FROM xml_response WHERE Scenario_id IN @ScenarioIds;
		""";

	private const string CreateStagingSql = """
		SELECT TOP (0) Scenario_id, Quote_ref, Product_code, Schem_code, XML_request, Test_tags
		INTO #RequestImport FROM xml_request;
		SELECT TOP (0) Scenario_id, XML_Response, Build_id, Status
		INTO #ResponseImport FROM xml_response;
		""";

	private const string InsertBatchSql = """
		IF EXISTS (
		    SELECT 1 FROM #RequestImport AS staged
		    INNER JOIN xml_request AS target WITH (UPDLOCK, HOLDLOCK)
		        ON target.Scenario_id = staged.Scenario_id)
		    THROW 50001, 'An imported request Scenario_id already exists.', 1;
		IF EXISTS (
		    SELECT 1 FROM #ResponseImport AS staged
		    INNER JOIN xml_response AS target WITH (UPDLOCK, HOLDLOCK)
		        ON target.Scenario_id = staged.Scenario_id)
		    THROW 50002, 'An imported response Scenario_id already exists.', 1;
		INSERT INTO xml_request
		    (Scenario_id, Quote_ref, Product_code, Schem_code, XML_request, Test_tags, Create_date)
		SELECT Scenario_id, Quote_ref, Product_code, Schem_code, XML_request, Test_tags, SYSUTCDATETIME()
		FROM #RequestImport;
		IF EXISTS (
		    SELECT 1 FROM #ResponseImport AS staged
		    LEFT JOIN xml_request AS request WITH (UPDLOCK, HOLDLOCK)
		        ON request.Scenario_id = staged.Scenario_id
		    WHERE request.Scenario_id IS NULL)
		    THROW 50003, 'An imported response has no matching request Scenario_id.', 1;
		INSERT INTO xml_response
		    (Scenario_id, XML_Response, Build_id, Status, Create_date, Last_updated)
		SELECT Scenario_id, XML_Response, Build_id, Status, SYSUTCDATETIME(), SYSUTCDATETIME()
		FROM #ResponseImport;
		""";

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
			return new ScenarioImportSnapshot([], []);
		}

		await using var connection = await connectionFactory.OpenAsync(cancellationToken);
		using var result = await connection.QueryMultipleAsync(new CommandDefinition(
			ReadExistingSql, new { ScenarioIds = scenarioIds }, cancellationToken: cancellationToken));
		var requests = (await result.ReadAsync<ScenarioRequestImport>()).AsList();
		var responses = (await result.ReadAsync<ScenarioResponseImport>()).AsList();
		return new ScenarioImportSnapshot(requests, responses);
	}

	public async Task InsertBatchAsync(
		IReadOnlyCollection<ScenarioRequestImport> requests,
		IReadOnlyCollection<ScenarioResponseImport> responses,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(requests);
		ArgumentNullException.ThrowIfNull(responses);
		if (requests.Count > 1000 || responses.Count > 1000)
		{
			throw new ArgumentOutOfRangeException(nameof(requests), "An insert batch cannot exceed 1000 rows per table.");
		}
		if (requests.Count == 0 && responses.Count == 0)
		{
			return;
		}

		await using var connection = await connectionFactory.OpenAsync(cancellationToken);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
		await connection.ExecuteAsync(new CommandDefinition(
			CreateStagingSql, transaction: transaction, cancellationToken: cancellationToken));

		using var requestTable = CreateTable("Scenario_id", "Quote_ref", "Product_code", "Schem_code", "XML_request", "Test_tags");
		foreach (var request in requests)
		{
			requestTable.Rows.Add(request.ScenarioId, request.QuoteRef, request.ProductCode,
				request.SchemeCode, request.XmlRequest, request.TestTags);
		}
		using var responseTable = CreateTable("Scenario_id", "XML_Response", "Build_id", "Status");
		foreach (var response in responses)
		{
			responseTable.Rows.Add(response.ScenarioId, response.XmlResponse, response.BuildId, response.Status);
		}

		await CopyAsync(connection, transaction, "#RequestImport", requestTable, cancellationToken);
		await CopyAsync(connection, transaction, "#ResponseImport", responseTable, cancellationToken);
		await connection.ExecuteAsync(new CommandDefinition(
			InsertBatchSql, transaction: transaction, commandTimeout: 120, cancellationToken: cancellationToken));
		await transaction.CommitAsync(cancellationToken);
	}

	private static DataTable CreateTable(params string[] columns)
	{
		var table = new DataTable();
		foreach (var column in columns)
		{
			table.Columns.Add(column, typeof(string));
		}
		return table;
	}

	private static async Task CopyAsync(
		SqlConnection connection,
		SqlTransaction transaction,
		string destination,
		DataTable table,
		CancellationToken cancellationToken)
	{
		using var copy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, transaction)
		{
			DestinationTableName = destination,
			BulkCopyTimeout = 120,
			BatchSize = 1000,
			EnableStreaming = true
		};
		foreach (DataColumn column in table.Columns)
		{
			copy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
		}
		using var reader = table.CreateDataReader();
		await copy.WriteToServerAsync(reader, cancellationToken);
	}
}