using Dapper;
using Microsoft.Data.SqlClient;

namespace FuzzyPricingMatcher.Tests.Database;

public interface ISqlMutationSessionFactory
{
    Task<ISqlMutationSession> OpenAsync(CancellationToken cancellationToken);
}

public interface ISqlMutationSession : IAsyncDisposable
{
    Task<ISqlMutationTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> ExecuteAsync(SqlCommandPlan plan, ISqlMutationTransaction? transaction, CancellationToken cancellationToken);
}

public interface ISqlMutationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

public sealed class SqlMutationExecutor
{
    private readonly ISqlMutationSessionFactory sessionFactory;

    public SqlMutationExecutor(ISqlMutationSessionFactory sessionFactory)
    {
        this.sessionFactory = sessionFactory;
    }

    public async Task ExecuteAsync(SqlCommandPlan plan, int expectedAffectedRows, CancellationToken cancellationToken)
    {
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        await ExecuteExpectedAsync(session, plan, expectedAffectedRows, cancellationToken);
    }

    public async Task ExecuteTransactionAsync(IReadOnlyList<SqlCommandPlan> plans, int expectedAffectedRows, CancellationToken cancellationToken)
    {
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        await using var transaction = await session.BeginTransactionAsync(cancellationToken);
        try
        {
            // Keep the command sequence inside one transaction. The database retry owner
            // retries this whole delegate, never an individual statement.
            foreach (var plan in plans)
                await ExecuteExpectedAsync(session, plan, expectedAffectedRows, cancellationToken, transaction);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task ExecuteExpectedAsync(ISqlMutationSession session, SqlCommandPlan plan, int expectedAffectedRows, CancellationToken cancellationToken, ISqlMutationTransaction? transaction = null)
    {
        var affectedRows = await session.ExecuteAsync(plan, transaction, cancellationToken);
        if (affectedRows != expectedAffectedRows)
            throw new DatabaseConsistencyException($"Expected {expectedAffectedRows} affected row(s), but database affected {affectedRows}.");
    }
}

public sealed class SqlConnectionMutationSessionFactory : ISqlMutationSessionFactory
{
    private readonly SqlConnectionFactory connectionFactory;

    public SqlConnectionMutationSessionFactory(SqlConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ISqlMutationSession> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.Create();
        try
        {
            await connection.OpenAsync(cancellationToken);
            return new SqlMutationSession(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

public sealed class SqlMutationSession : ISqlMutationSession
{
    private readonly SqlConnection connection;

    public SqlMutationSession(SqlConnection connection)
    {
        this.connection = connection;
    }

    public async Task<ISqlMutationTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => new SqlMutationTransaction((SqlTransaction)await connection.BeginTransactionAsync(cancellationToken));

    public Task<int> ExecuteAsync(SqlCommandPlan plan, ISqlMutationTransaction? transaction, CancellationToken cancellationToken)
    {
        var sqlTransaction = transaction is SqlMutationTransaction concrete ? concrete.Transaction : null;
        return connection.ExecuteAsync(new CommandDefinition(plan.Sql, plan.Parameters, sqlTransaction, cancellationToken: cancellationToken));
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}

public sealed class SqlMutationTransaction : ISqlMutationTransaction
{
    internal SqlTransaction Transaction { get; }

    public SqlMutationTransaction(SqlTransaction transaction)
    {
        Transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => Transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => Transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => Transaction.DisposeAsync();
}
