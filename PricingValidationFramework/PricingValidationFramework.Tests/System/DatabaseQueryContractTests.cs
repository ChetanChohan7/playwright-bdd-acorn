using System.Reflection;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;

namespace PricingValidationFramework.Tests.System;

[TestFixture]
public class DatabaseQueryContractTests
{
    [Test]
    public void Connection_factory_should_create_a_closed_connection()
    {
        using var connection = new SqlConnectionFactory(new DatabaseSettings()).Create();

        Assert.That(connection.State, Is.EqualTo(global::System.Data.ConnectionState.Closed));
    }

    [Test]
    public void Connection_factory_should_fail_to_open_without_a_connection_string()
    {
        var factory = new SqlConnectionFactory(new DatabaseSettings());

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var connection = await factory.OpenAsync();
        });
    }

    [TestCase("AllScenariosSql")]
    [TestCase("TaggedScenariosSql")]
    [TestCase("ScenarioByIdSql")]
    public void Request_queries_should_use_the_current_table_columns(string queryName)
    {
        var sql = ReadQuery<RequestDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM xml_request"));
            Assert.That(sql, Does.Contain("Schem_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("Create_date AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Request_filters_should_remain_parameterized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadQuery<RequestDataReader>("TaggedScenariosSql"), Does.Contain("Test_tags = @TestTag"));
            Assert.That(ReadQuery<RequestDataReader>("ScenarioByIdSql"), Does.Contain("Scenario_id = @ScenarioId"));
        });
    }

    [TestCase("PassingBaselinesSql")]
    [TestCase("PassingBaselineByIdSql")]
    public void Baseline_queries_should_join_request_identifiers_and_retain_the_pass_filter(string queryName)
    {
        var sql = ReadQuery<BaselineDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM xml_response AS response"));
            Assert.That(sql, Does.Contain("INNER JOIN xml_request AS request ON request.Scenario_id = response.Scenario_id"));
            Assert.That(sql, Does.Contain("request.Quote_ref AS QuoteRef"));
            Assert.That(sql, Does.Contain("response.XML_Response AS XmlResponse"));
            Assert.That(sql, Does.Contain("response.Status = 'PASS'"));
            Assert.That(sql, Does.Not.Contain("response.Quote_ref"));
            Assert.That(sql, Does.Not.Contain("response.Product_code"));
            Assert.That(sql, Does.Not.Contain("response.Schem_code"));
            Assert.That(sql, Does.Not.Contain("Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Ice_selection_should_keep_the_latest_passing_baseline_per_request_product_and_scheme()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselinesSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("request.Product_code AS ProductCode"));
            Assert.That(sql, Does.Contain("request.Schem_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("PARTITION BY request.Product_code, request.Schem_code"));
            Assert.That(sql, Does.Contain("ORDER BY response.Last_updated DESC, response.Scenario_id DESC"));
            Assert.That(sql, Does.Contain("WHERE RowNumber = 1"));
        });
    }

    [Test]
    public void Radar_baseline_selection_should_map_audit_fields_and_filter_by_scenario_id()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselineByIdSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("response.Create_date AS CreatedDate"));
            Assert.That(sql, Does.Contain("response.Last_updated AS LastUpdated"));
            Assert.That(sql, Does.Contain("response.Build_id AS BuildId"));
            Assert.That(sql, Does.Contain("response.Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Result_update_should_preserve_creation_date_and_update_status_response_build_and_timestamp()
    {
        var sql = ReadQuery<ResultUpdater>("UpdateResultSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE xml_response"));
            Assert.That(sql, Does.Contain("Status = @Status"));
            Assert.That(sql, Does.Contain("Build_id = @BuildId"));
            Assert.That(sql, Does.Contain("XML_Response = @XmlResponse"));
            Assert.That(sql, Does.Contain("Last_updated = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Quote_ref"));
            Assert.That(sql, Does.Not.Contain("Product_code"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
        });
    }

    [Test]
    public void Import_repository_should_stage_the_current_columns_and_link_responses_by_scenario_id()
    {
        var sql = ReadQuery<ScenarioImportRepository>("InsertBatchSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("INSERT INTO xml_request"));
            Assert.That(sql, Does.Contain("INSERT INTO xml_response"));
            Assert.That(sql, Does.Contain("ON request.Scenario_id = staged.Scenario_id"));
            Assert.That(sql, Does.Contain("Schem_code"));
            Assert.That(sql, Does.Contain("Create_date, Last_updated"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("UPDLOCK, HOLDLOCK"));
            Assert.That(sql, Does.Not.Contain("UPDATE xml_response"));
        });
    }

    private static string ReadQuery<TReader>(string fieldName)
    {
        var field = typeof(TReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetRawConstantValue() as string
            ?? throw new AssertionException($"SQL constant '{typeof(TReader).Name}.{fieldName}' was not found.");
    }
}