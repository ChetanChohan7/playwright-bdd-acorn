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
            Assert.That(sql, Does.Contain("Scheme_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("Create_date AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
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
    [TestCase("BaselineByIdSql")]
    public void Baseline_queries_should_join_request_identifiers(string queryName)
    {
        var sql = ReadQuery<BaselineDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM xml_response AS response"));
            Assert.That(sql, Does.Contain("INNER JOIN xml_request AS request ON request.Scenario_id = response.Scenario_id"));
            Assert.That(sql, Does.Contain("request.Quote_ref AS QuoteRef"));
            Assert.That(sql, Does.Contain("response.XML_Response AS XmlResponse"));
            Assert.That(sql, Does.Not.Contain("response.Quote_ref"));
            Assert.That(sql, Does.Not.Contain("response.Product_code"));
            Assert.That(sql, Does.Not.Contain("response.Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Ice_selection_should_keep_the_latest_passing_baseline_per_request_product_and_scheme()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselinesSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("response.Status = 'PASS'"));
            Assert.That(sql, Does.Contain("request.Product_code AS ProductCode"));
            Assert.That(sql, Does.Contain("request.Scheme_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("PARTITION BY request.Product_code, request.Scheme_code"));
            Assert.That(sql, Does.Contain("ORDER BY response.Last_updated DESC, response.Scenario_id DESC"));
            Assert.That(sql, Does.Contain("WHERE RowNumber = 1"));
        });
    }

    [Test]
    public void Radar_baseline_selection_should_map_audit_fields_and_filter_by_scenario_id()
    {
        var sql = ReadQuery<BaselineDataReader>("BaselineByIdSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("response.Status AS Status"));
            Assert.That(sql, Does.Not.Contain("Status = 'PASS'"));
            Assert.That(sql, Does.Contain("response.Create_date AS CreatedDate"));
            Assert.That(sql, Does.Contain("response.Last_updated AS LastUpdated"));
            Assert.That(sql, Does.Contain("response.Build_id AS BuildId"));
            Assert.That(sql, Does.Contain("response.Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Passing_result_update_should_preserve_creation_date_and_update_status_response_build_and_timestamp()
    {
        var sql = ReadQuery<ResultUpdater>("UpdatePassingResultSql");

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
            Assert.That(sql, Does.Not.Contain("Scheme_code"));
        });
    }

    [Test]
    public void Failed_result_update_should_change_only_the_status_and_build()
    {
        var sql = ReadQuery<ResultUpdater>("UpdateFailedStatusSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE xml_response"));
            Assert.That(sql, Does.Contain("SET Status = @Status"));
            Assert.That(sql, Does.Contain("Build_id = @BuildId"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("XML_Response"));
            Assert.That(sql, Does.Not.Contain("Last_updated"));
        });
    }

    [Test]
    public void Baseline_insert_should_never_replace_an_existing_baseline()
    {
        var sql = ReadQuery<ResultUpdater>("InsertBaselineSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("INSERT INTO xml_response (Scenario_id, XML_Response, Build_id, Status, Create_date, Last_updated)"));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"));
            Assert.That(sql, Does.Contain("WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Not.Contain("UPDATE"));
        });
    }

    [Test]
    public void Import_insert_should_use_parameterised_values_rows_guarded_against_existing_ids()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(2);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("INSERT INTO xml_request (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags, Create_date)"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("(@s0, @q0, @p0, @c0, @x0, @t0)"));
            Assert.That(sql, Does.Contain("(@s1, @q1, @p1, @c1, @x1, @t1)"));
            Assert.That(sql, Does.Contain("AS v (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags)"));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"));
            Assert.That(sql, Does.Contain("xml_request AS target WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Contain("WHERE target.Scenario_id = v.Scenario_id"));
        });
    }

    [Test]
    public void Import_insert_should_need_only_insert_and_select_on_existing_tables()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(ScenarioImportRepository.MaxRowsPerStatement);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("#"), "no temporary tables");
            Assert.That(sql, Does.Not.Match(@"\bCREATE\s").IgnoreCase, "creates nothing (Create_date is only a column)");
            Assert.That(sql, Does.Not.Contain("MERGE").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("OPENJSON").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("xml_response"));
            Assert.That(sql, Does.Not.Contain("UPDATE").IgnoreCase);
        });
    }

    [Test]
    public void Import_insert_should_stay_within_sql_servers_parameter_limit()
    {
        var sql = ScenarioImportRepository.BuildInsertSql(ScenarioImportRepository.MaxRowsPerStatement);
        var parameters = global::System.Text.RegularExpressions.Regex.Matches(sql, @"@[a-z]\d+")
            .Select(match => match.Value)
            .Distinct()
            .Count();

        Assert.Multiple(() =>
        {
            Assert.That(ScenarioImportRepository.MaxRowsPerStatement, Is.EqualTo(333));
            Assert.That(parameters, Is.EqualTo(ScenarioImportRepository.MaxRowsPerStatement * ScenarioImportRepository.ParametersPerRow));
            Assert.That(parameters, Is.LessThanOrEqualTo(2100));
        });
    }

    [TestCase(0)]
    [TestCase(334)]
    public void Import_insert_should_reject_statement_sizes_outside_the_limit(int rowCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioImportRepository.BuildInsertSql(rowCount));
    }

    private static string ReadQuery<TReader>(string fieldName)
    {
        var field = typeof(TReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetRawConstantValue() as string
            ?? throw new AssertionException($"SQL constant '{typeof(TReader).Name}.{fieldName}' was not found.");
    }
}