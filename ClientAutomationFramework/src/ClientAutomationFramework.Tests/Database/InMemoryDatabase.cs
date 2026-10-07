using System.Data;
using System.Data.Common;
using System.Globalization;
using ClientAutomationFramework.Core.Configuration;
using ClientAutomationFramework.Core.Database;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClientAutomationFramework.Tests.Database;

/// A fresh SQL Server-shaped database for one test, backed by SQLite's shared-cache in-memory
/// mode instead of a real server: builds the two tables on construction, runs the test against
/// them, and drops everything on Dispose.
///
/// Each call through IDbConnectionFactory.OpenAsync (matching the real SqlConnectionFactory's
/// "new connection per call" contract) opens its own SqliteConnection - a plain ":memory:"
/// database would be wiped the moment that connection closed. The "Cache=Shared" connection
/// string keeps every connection opened with the same DataSource pointed at one shared database,
/// and the KeepAlive connection held open for the lifetime of this instance is what stops that
/// shared database from being deallocated between calls.
///
/// The schema below is a SQLite-dialect copy of sql/001_create_tables.sql (TOP/IDENTITY/
/// NVARCHAR/SYSUTCDATETIME have no SQLite equivalent) - the production schema is the source of
/// truth; keep this in sync if it changes.
public sealed class InMemoryDatabase : IDbConnectionFactory, IAsyncDisposable
{
    private const string Schema = """
        CREATE TABLE XML_Requests
        (
            Scenario_id  TEXT NOT NULL,
            Quote_ref    TEXT NOT NULL,
            XML_Request  TEXT NOT NULL,
            PRIMARY KEY (Scenario_id, Quote_ref)
        );

        CREATE TABLE XML_Response
        (
            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
            Scenario_id   TEXT NOT NULL,
            Quote_ref     TEXT NOT NULL,
            XML_Response  TEXT NOT NULL,
            Status        TEXT NOT NULL CHECK (Status IN ('PASS', 'FAIL')),
            Created_date  TEXT NOT NULL
        );

        CREATE INDEX IX_XML_Response_Scenario_Created ON XML_Response (Scenario_id, Created_date DESC, Id DESC);
        """;

    static InMemoryDatabase() => SqlMapper.AddTypeHandler(new SqliteDateTimeHandler());

    private readonly string connectionString;
    private readonly SqliteConnection keepAlive;

    private InMemoryDatabase(string connectionString, SqliteConnection keepAlive)
    {
        this.connectionString = connectionString;
        this.keepAlive = keepAlive;
    }

    public static async Task<InMemoryDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connectionString =
            new SqliteConnectionStringBuilder { DataSource = $"InMemoryDatabase-{Guid.NewGuid()}", Mode = SqliteOpenMode.Memory, Cache = SqliteCacheMode.Shared }.ToString();

        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync(cancellationToken);

        await using (var command = keepAlive.CreateCommand())
        {
            command.CommandText = Schema;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new InMemoryDatabase(connectionString, keepAlive);
    }

    public DatabaseSettings Settings { get; } = new();

    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public ValueTask DisposeAsync() => keepAlive.DisposeAsync();

    /// SQLite has no native datetime storage class - Created_date round-trips as TEXT, so
    /// Microsoft.Data.Sqlite reports its reader column type as string, not DateTime. Without
    /// this, Dapper can't materialize ScenarioResponse (a positional record, so its one
    /// constructor must match column types exactly with no property-setter fallback available).
    private sealed class SqliteDateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value) => parameter.Value = value;

        public override DateTime Parse(object value) => value switch
        {
            DateTime dateTime => dateTime,
            string text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => throw new InvalidCastException($"Cannot read '{value}' ({value.GetType()}) as a DateTime.")
        };
    }
}
