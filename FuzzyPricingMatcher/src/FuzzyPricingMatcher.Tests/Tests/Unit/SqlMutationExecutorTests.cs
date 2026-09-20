using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Data;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class SqlMutationExecutorTests
{
    [Test]
    public async Task Transaction_commits_once_after_all_commands_succeed()
    {
        var session = new RecordingSession(1, 1);
        await new SqlMutationExecutor(new RecordingSessionFactory(session)).ExecuteTransactionAsync(Plans(), 1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(session.Commands.Count, Is.EqualTo(2));
            Assert.That(session.BeginCount, Is.EqualTo(1));
            Assert.That(session.Transaction.CommitCount, Is.EqualTo(1));
            Assert.That(session.Transaction.RollbackCount, Is.Zero);
            Assert.That(session.Commands.All(command => command.Transaction is not null), Is.True);
        });
    }

    [Test]
    public async Task Failure_during_second_command_rolls_back_without_commit()
    {
        var session = new RecordingSession(1, 0);
        var executor = new SqlMutationExecutor(new RecordingSessionFactory(session));

        Assert.That(async () => await executor.ExecuteTransactionAsync(Plans(), 1, CancellationToken.None), Throws.TypeOf<DatabaseConsistencyException>());
        Assert.Multiple(() =>
        {
            Assert.That(session.Commands.Count, Is.EqualTo(2));
            Assert.That(session.Transaction.CommitCount, Is.Zero);
            Assert.That(session.Transaction.RollbackCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Affected_row_mismatch_prevents_commit()
    {
        var session = new RecordingSession(2, 1);
        var executor = new SqlMutationExecutor(new RecordingSessionFactory(session));

        Assert.That(async () => await executor.ExecuteTransactionAsync(Plans(), 1, CancellationToken.None), Throws.TypeOf<DatabaseConsistencyException>());
        Assert.That(session.Transaction.CommitCount, Is.Zero);
        Assert.That(session.Transaction.RollbackCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Retry_restarts_the_complete_transaction_unit()
    {
        var first = new RecordingSession(new[] { 1, 1 }, new TimeoutException());
        var second = new RecordingSession(1, 1);
        var sessions = new RecordingSessionFactory(first, second);
        var retry = new DatabaseRetryExecutor(new ResilienceSettings { SqlRetryAttempts = 2 }, (_, _) => Task.CompletedTask);
        var executor = new SqlMutationExecutor(sessions);

        await retry.ExecuteAsync("transaction", token => executor.ExecuteTransactionAsync(Plans(), 1, token));

        Assert.Multiple(() =>
        {
            Assert.That(sessions.OpenCount, Is.EqualTo(2));
            Assert.That(first.Commands.Count, Is.EqualTo(2));
            Assert.That(second.Commands.Count, Is.EqualTo(2));
            Assert.That(first.Transaction.RollbackCount, Is.EqualTo(1));
            Assert.That(second.Transaction.CommitCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Command_plans_preserve_parameters_and_loader_status_rules()
    {
        var factory = new SqlCommandPlanFactory("dbo.tb_xmlrequest", "dbo.tb_response");
        var insert = factory.InsertBaseline(new DatabaseInsertCommand("SCN", "Q", "<request/>", "tag", "<response/>", "BUILD"));
        var update = factory.UpdateBaseline(new DatabaseUpdateCommand("SCN", "Q", "<request/>", "tag", "<response/>", "BUILD"));
        var tags = factory.UpdateTags(new DatabaseTagsUpdateCommand("SCN", "tag2"));
        var pass = factory.UpdateComparisonPass(new DatabaseComparisonPassCommand("SCN", "<response/>", "BUILD"));
        var fail = factory.UpdateComparisonFail(new DatabaseComparisonFailCommand("SCN", "BUILD"));

        Assert.Multiple(() =>
        {
            Assert.That(insert[0].Sql, Does.Contain("@ScenarioId").And.Contain("@QuoteRef").And.Contain("@XmlRequest").And.Contain("@TestTags"));
            Assert.That(insert[1].Sql, Does.Contain("@XmlResponse").And.Contain("Status) VALUES").And.Contain("NULL, NULL"));
            Assert.That(update[1].Sql, Does.Contain("XML_response = @XmlResponse").And.Contain("Status = NULL"));
            Assert.That(tags.Sql, Does.Not.Contain("tb_response"));
            Assert.That(pass.Sql, Does.Contain("XML_response = @XmlResponse"));
            Assert.That(fail.Sql, Does.Not.Contain("XML_response ="));
            Assert.That(((DatabaseInsertCommand)insert[0].Parameters).XmlRequest, Is.EqualTo("<request/>"));
            Assert.That(((DatabaseInsertCommand)insert[1].Parameters).XmlResponse, Is.EqualTo("<response/>"));
            Assert.That(((DatabaseUpdateCommand)update[1].Parameters).BuildId, Is.EqualTo("BUILD"));
            Assert.That(((DatabaseTagsUpdateCommand)tags.Parameters).TestTags, Is.EqualTo("tag2"));
        });
    }

    [Test]
    public void Obsolete_deletion_plans_are_response_before_request()
    {
        var plans = new SqlCommandPlanFactory("dbo.tb_xmlrequest", "dbo.tb_response").DeleteObsolete("OLD");

        Assert.Multiple(() =>
        {
            Assert.That(plans[0].Sql, Does.Contain("DELETE FROM dbo.tb_response"));
            Assert.That(plans[1].Sql, Does.Contain("DELETE FROM dbo.tb_xmlrequest"));
            Assert.That(((dynamic)plans[0].Parameters).ScenarioId, Is.EqualTo("OLD"));
            Assert.That(((dynamic)plans[1].Parameters).ScenarioId, Is.EqualTo("OLD"));
        });
    }

    private static IReadOnlyList<SqlCommandPlan> Plans() => new[]
    {
        new SqlCommandPlan("request", new { ScenarioId = "SCN", XmlRequest = "<request/>" }),
        new SqlCommandPlan("response", new { ScenarioId = "SCN", XmlResponse = "<response/>" })
    };

    private sealed class RecordingSessionFactory : ISqlMutationSessionFactory
    {
        private readonly Queue<RecordingSession> sessions;
        public int OpenCount { get; private set; }

        public RecordingSessionFactory(params RecordingSession[] sessions) => this.sessions = new(sessions);

        public Task<ISqlMutationSession> OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.FromResult<ISqlMutationSession>(sessions.Dequeue());
        }
    }

    private sealed class RecordingSession : ISqlMutationSession
    {
        private readonly Queue<int> affectedRows;
        private readonly Exception? secondCommandException;
        public List<(SqlCommandPlan Plan, ISqlMutationTransaction? Transaction)> Commands { get; } = [];
        public RecordingTransaction Transaction { get; } = new();
        public int BeginCount { get; private set; }

        public RecordingSession(params int[] affectedRows) => this.affectedRows = new(affectedRows);

        public RecordingSession(int[] affectedRows, Exception secondCommandException) : this(affectedRows)
        {
            this.secondCommandException = secondCommandException;
        }

        public Task<ISqlMutationTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.FromResult<ISqlMutationTransaction>(Transaction);
        }

        public Task<int> ExecuteAsync(SqlCommandPlan plan, ISqlMutationTransaction? transaction, CancellationToken cancellationToken)
        {
            Commands.Add((plan, transaction));
            if (Commands.Count == 2 && secondCommandException is not null)
                throw secondCommandException;
            return Task.FromResult(affectedRows.Dequeue());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingTransaction : ISqlMutationTransaction
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public Task CommitAsync(CancellationToken cancellationToken) { CommitCount++; return Task.CompletedTask; }
        public Task RollbackAsync(CancellationToken cancellationToken) { RollbackCount++; return Task.CompletedTask; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
