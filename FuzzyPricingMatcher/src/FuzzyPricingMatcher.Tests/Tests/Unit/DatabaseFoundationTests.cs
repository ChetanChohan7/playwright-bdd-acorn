using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Database;
using FuzzyPricingMatcher.Tests.Loader;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class DatabaseFoundationTests
{
    [TestCase("dbo.tb_xmlrequest")]
    [TestCase("schema_1.table_2")]
    [Category("Unit")]
    public void Safe_schema_qualified_identifiers_are_accepted(string identifier)
    {
        Assert.That(SafeSqlIdentifierValidator.Validate(identifier), Is.EqualTo(identifier));
    }

    [TestCase("dbo.tb_response; DROP TABLE dbo.tb_response")]
    [TestCase("dbo.tb response")]
    [TestCase("tb_response")]
    [TestCase("dbo.[tb_response]")]
    [Category("Unit")]
    public void Unsafe_or_unqualified_identifiers_are_rejected(string identifier)
    {
        Assert.That(() => SafeSqlIdentifierValidator.Validate(identifier), Throws.TypeOf<DatabaseOperationException>());
    }

    [Test]
    [Category("Unit")]
    public void Empty_connection_string_is_rejected_before_connection_creation()
    {
        Assert.That(() => new SqlConnectionFactory(new DatabaseSettings()), Throws.TypeOf<DatabaseOperationException>());
    }

    [Test]
    [Category("Unit")]
    public async Task Transient_failures_retry_with_configured_attempts()
    {
        var attempts = 0;
        var delays = 0;
        var settings = new ResilienceSettings { SqlRetryAttempts = 3, SqlRetryInitialDelaySeconds = 1, SqlRetryMaximumDelaySeconds = 2 };
        var retry = new DatabaseRetryExecutor(settings, (_, _) => { delays++; return Task.CompletedTask; });
        var result = await retry.ExecuteAsync("test-operation", _ =>
        {
            attempts++;
            if (attempts < 3) throw new TimeoutException("transient");
            return Task.FromResult("ok");
        });
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(delays, Is.EqualTo(2));
        });
    }

    [Test]
    [Category("Unit")]
    public void Command_models_express_required_transaction_intent_without_database_access()
    {
        var insert = new DatabaseInsertCommand("SCN", "Q", "<request />", "smoke", "<response />", "BUILD");
        var update = new DatabaseUpdateCommand("SCN", "Q2", "<request new />", "pricing", "<response new />", "BUILD2");
        var delete = new DeleteObsoleteScenarioCommand(new[] { "OLD" });
        Assert.Multiple(() =>
        {
            Assert.That(insert.XmlRequest, Is.EqualTo("<request />"));
            Assert.That(update.QuoteRef, Is.EqualTo("Q2"));
            Assert.That(delete.ScenarioIds, Is.EqualTo(new[] { "OLD" }));
        });
    }

    [Test]
    [Category("Unit")]
    public async Task Non_transient_failure_is_not_retried()
    {
        var attempts = 0;
        var retry = new DatabaseRetryExecutor(new ResilienceSettings { SqlRetryAttempts = 5 }, (_, _) => Task.CompletedTask);
        Assert.That(async () => await retry.ExecuteAsync("test-operation", _ =>
        {
            attempts++;
            throw new InvalidOperationException("logical failure");
        }), Throws.TypeOf<InvalidOperationException>());
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    [Category("Unit")]
    public void Prompt_5_does_not_open_a_live_database_connection()
    {
        Assert.That(() => new SqlConnectionFactory(new DatabaseSettings { SqlConnectionString = "" }), Throws.TypeOf<DatabaseOperationException>());
    }

    [Test]
    [Category("Unit")]
    public void SQL_mutation_plans_parameterize_values_and_preserve_transaction_order()
    {
        var factory = new SqlCommandPlanFactory("dbo.tb_xmlrequest", "dbo.tb_response");
        var insert = factory.InsertBaseline(new DatabaseInsertCommand("SCN", "Q", "<request />", "smoke", "<response />", "BUILD"));
        var update = factory.UpdateBaseline(new DatabaseUpdateCommand("SCN", "Q2", "<request new />", "pricing", "<response new />", "BUILD2"));
        var tags = factory.UpdateTags(new DatabaseTagsUpdateCommand("SCN", "smoke"));
        var pass = factory.UpdateComparisonPass(new DatabaseComparisonPassCommand("SCN", "<response pass />", "BUILD3"));
        var fail = factory.UpdateComparisonFail(new DatabaseComparisonFailCommand("SCN", "BUILD4"));
        var delete = factory.DeleteObsolete("OLD");
        Assert.Multiple(() =>
        {
            Assert.That(insert, Has.Count.EqualTo(2));
            Assert.That(insert[0].Sql, Does.Contain("@ScenarioId").And.Contain("@QuoteRef").And.Contain("@XmlRequest").And.Contain("@TestTags"));
            Assert.That(insert[1].Sql, Does.Contain("@XmlResponse").And.Contain("@BuildId").And.Contain("Status) VALUES").And.Contain("NULL, NULL"));
            Assert.That(update[1].Sql, Does.Contain("XML_response = @XmlResponse").And.Contain("Status = NULL").And.Contain("Last_updated"));
            Assert.That(tags.Sql, Does.Contain("SET Test_tags = @TestTags").And.Not.Contain("XML_response"));
            Assert.That(pass.Sql, Does.Contain("XML_response = @XmlResponse").And.Contain("Status = 'Pass'"));
            Assert.That(fail.Sql, Does.Not.Contain("XML_response").And.Contain("Status = 'Fail'"));
            Assert.That(delete[0].Sql, Does.Contain("tb_response"));
            Assert.That(delete[1].Sql, Does.Contain("tb_xmlrequest"));
            Assert.That(insert.Concat(update).Select(plan => plan.Sql), Is.All.Matches<string>(sql => sql.Contains("@ScenarioId", StringComparison.Ordinal)));
        });
    }
}
