namespace PricingValidationFramework.Core.Database;

using System.Data;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;

public interface IScenarioStatusUpdater
{
	Task<IReadOnlyList<ScenarioStatusChange>> SetStatusAsync(
		IReadOnlyCollection<string> scenarioIds,
		string status,
		CancellationToken cancellationToken = default);
}

/// Sets [Xml].[Response].Status to PASS or FAIL for given scenarios, e.g. after someone has reviewed a
/// FAIL. Changes Status and the row_modified* audit columns only: never XML_Response (the baseline),
/// never Last_updated (ICE picks its baseline by it), and never inserts or deletes rows.
public sealed class ScenarioStatusUpdater : IScenarioStatusUpdater
{
	public static readonly IReadOnlySet<string> AllowedStatuses = new HashSet<string>(StringComparer.Ordinal) { "PASS", "FAIL" };

	// IN @ScenarioIds becomes one parameter per ID; stay well under SQL Server's 2,100.
	internal const int MaxScenarioIdsPerStatement = 1000;

	// Read the current status first (locked for the transaction) so each ID can be reported as
	// updated, already at that status, or not found.
	private const string ReadStatusSql = """
		SELECT Scenario_id AS ScenarioId, Status
		FROM [Xml].[Response] WITH (UPDLOCK, HOLDLOCK)
		WHERE Scenario_id IN @ScenarioIds;
		""";

	// Rows already at the requested status are left alone, so their audit columns keep the last real change.
	private const string UpdateStatusSql = """
		UPDATE [Xml].[Response]
		SET Status = @Status,
		    row_modifieddate = SYSUTCDATETIME(),
		    row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())
		WHERE Scenario_id IN @ScenarioIds
		  AND (Status IS NULL OR Status <> @Status);
		""";

	private readonly SqlConnectionFactory connectionFactory;
	private readonly RetrySettings retrySettings;

	public ScenarioStatusUpdater(SqlConnectionFactory connectionFactory, RetrySettings? retrySettings = null)
	{
		this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		this.retrySettings = retrySettings ?? new RetrySettings();
		this.retrySettings.ValidateDatabaseRetrySettings();
	}

	public async Task<IReadOnlyList<ScenarioStatusChange>> SetStatusAsync(
		IReadOnlyCollection<string> scenarioIds,
		string status,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(scenarioIds);
		if (!AllowedStatuses.Contains(status))
		{
			throw new ArgumentException("Status must be PASS or FAIL.", nameof(status));
		}

		var changes = new List<ScenarioStatusChange>(scenarioIds.Count);
		foreach (var chunk in scenarioIds.Chunk(MaxScenarioIdsPerStatement))
		{
			changes.AddRange(await SetChunkAsync(chunk, status, cancellationToken));
		}

		return changes;
	}

	// One transaction per chunk: a chunk is either fully applied or not at all, and is safe to retry.
	private async Task<IReadOnlyList<ScenarioStatusChange>> SetChunkAsync(
		string[] scenarioIds,
		string status,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				await using var connection = await connectionFactory.OpenAsync(cancellationToken);
				await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
				var current = (await connection.QueryAsync<(string ScenarioId, string? Status)>(new CommandDefinition(
						ReadStatusSql, new { ScenarioIds = scenarioIds }, transaction, cancellationToken: cancellationToken)))
					.ToDictionary(row => row.ScenarioId, row => row.Status, StringComparer.OrdinalIgnoreCase);
				await connection.ExecuteAsync(new CommandDefinition(
					UpdateStatusSql,
					new { ScenarioIds = scenarioIds, Status = status, connectionFactory.AuditUser },
					transaction,
					cancellationToken: cancellationToken));
				await transaction.CommitAsync(cancellationToken);

				return scenarioIds.Select(id => !current.TryGetValue(id, out var previous)
						? new ScenarioStatusChange(id, null, status, ScenarioStatusOutcome.NotFound)
						: string.Equals(previous, status, StringComparison.Ordinal)
							? new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.AlreadySet)
							: new ScenarioStatusChange(id, previous, status, ScenarioStatusOutcome.Updated))
					.ToArray();
			}
			catch (SqlException) when (attempt < retrySettings.DatabaseRetryCount)
			{
				await Task.Delay(TimeSpan.FromSeconds(retrySettings.DatabaseRetryDelaySeconds * Math.Pow(2, attempt)), cancellationToken);
			}
		}
	}
}

[JsonConverter(typeof(JsonStringEnumConverter<ScenarioStatusOutcome>))]
public enum ScenarioStatusOutcome
{
	Updated,
	AlreadySet,
	NotFound
}

/// What happened to one scenario. PreviousStatus is null when the scenario has no [Xml].[Response] row.
public sealed record ScenarioStatusChange(
	string ScenarioId,
	string? PreviousStatus,
	string NewStatus,
	ScenarioStatusOutcome Outcome);
