using System.Reflection;
using PricingValidationFramework.Core.Configuration;
using PricingValidationFramework.Core.Database;
using PricingValidationFramework.Core.Models.Database;

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
    [TestCase("ProductScenariosSql")]
    [TestCase("ScenarioByIdSql")]
    [TestCase("ScenariosWithoutBaselineSql")]
    [TestCase("FilteredScenariosSql")]
    public void Request_queries_should_use_the_current_table_columns(string queryName)
    {
        var sql = ReadQuery<RequestDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Request]"));
            Assert.That(sql, Does.Contain("Scheme_code AS SchemeCode"));
            Assert.That(sql, Does.Contain("row_creationdate AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Schem_code"));
            Assert.That(sql, Does.Not.Contain("Created_date"));
        });
    }

    [Test]
    public void Seed_query_should_select_only_requests_with_no_response_row_of_any_status()
    {
        var sql = ReadQuery<RequestDataReader>("ScenariosWithoutBaselineSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS (SELECT 1 FROM [Xml].[Response] AS response WHERE response.Scenario_id = request.Scenario_id)"));
            Assert.That(sql, Does.Not.Contain("Status"));
            Assert.That(sql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
            Assert.That(sql, Does.Not.Match(@"\b(INSERT|UPDATE|DELETE|CREATE)\s"));
        });
    }

    [Test]
    public void Request_filters_should_remain_parameterized()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadQuery<RequestDataReader>("ProductScenariosSql"), Does.Contain("Product_code = @ProductCode"));
            Assert.That(ReadQuery<RequestDataReader>("ScenarioByIdSql"), Does.Contain("Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Radar_product_filter_should_preserve_all_schemes_and_tags_as_metadata()
    {
        var productSql = ReadQuery<RequestDataReader>("ProductScenariosSql");
        var allSql = ReadQuery<RequestDataReader>("AllScenariosSql");

        Assert.Multiple(() =>
        {
            Assert.That(productSql, Does.Contain("WHERE Product_code = @ProductCode ORDER BY row_creationdate, Scenario_id;"));
            Assert.That(productSql, Does.Contain("Test_tags AS TestTags"));
            Assert.That(productSql, Does.Not.Contain("Test_tags ="));
            Assert.That(productSql, Does.Not.Contain("@TestTag"));
            Assert.That(productSql, Does.Not.Contain("Scheme_code ="));
            Assert.That(productSql, Does.Not.Contain("ROW_NUMBER"));
            Assert.That(productSql, Does.Not.Contain("[Xml].[Response]"));
            Assert.That(allSql, Does.Not.Contain("WHERE"));
            Assert.That(allSql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
        });
    }

    [TestCase("PassingBaselinesSql")]
    [TestCase("FilteredPassingBaselinesSql")]
    [TestCase("PassingBaselinesByIdSql")]
    [TestCase("BaselineByIdSql")]
    public void Baseline_queries_should_join_request_identifiers(string queryName)
    {
        var sql = ReadQuery<BaselineDataReader>(queryName);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Response] AS response"));
            Assert.That(sql, Does.Contain("INNER JOIN [Xml].[Request] AS request ON request.Scenario_id = response.Scenario_id"));
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
    public void Ice_filtered_selection_should_be_the_same_query_with_optional_filters_inside_the_ranking()
    {
        var all = ReadQuery<BaselineDataReader>("PassingBaselinesSql");
        var filtered = ReadQuery<BaselineDataReader>("FilteredPassingBaselinesSql");
        var filters = "\n\t\t\tAND (@ProductCode IS NULL OR request.Product_code = @ProductCode)" +
            "\n\t\t\tAND (@ScenarioTag IS NULL OR CHARINDEX(',' + @ScenarioTag + ',', ',' + REPLACE(REPLACE(ISNULL(request.Test_tags, ''), ';', ','), ' ', '') + ',') > 0)";

        Assert.Multiple(() =>
        {
            Assert.That(filtered, Does.Contain("WHERE response.Status = 'PASS'" + filters));
            Assert.That(filtered.IndexOf("@ScenarioTag", StringComparison.Ordinal), Is.LessThan(filtered.IndexOf("WHERE RowNumber = 1", StringComparison.Ordinal)));
            Assert.That(filtered.Replace(filters, string.Empty, StringComparison.Ordinal), Is.EqualTo(all));
            Assert.That(all, Does.Not.Contain("@ProductCode"));
        });
    }

    [Test]
    public void Ice_selection_by_scenario_id_should_take_exactly_those_passing_baselines_without_ranking()
    {
        var sql = ReadQuery<BaselineDataReader>("PassingBaselinesByIdSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE response.Status = 'PASS' AND response.Scenario_id IN @ScenarioIds;"));
            Assert.That(sql, Does.Not.Contain("ROW_NUMBER"));
            Assert.That(BaselineDataReader.MaxScenarioIdsPerQuery, Is.LessThanOrEqualTo(2000), "IN expands to one parameter per ID");
        });
    }

    [Test]
    public void Radar_filters_should_be_optional_parameterized_and_match_one_whole_tag()
    {
        var sql = ReadQuery<RequestDataReader>("FilteredScenariosSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE (@ProductCode IS NULL OR Product_code = @ProductCode)"));
            Assert.That(sql, Does.Contain("AND (@ScenarioTag IS NULL OR " + RequestDataReader.ScenarioTagMatch + ")"));
            Assert.That(RequestDataReader.ScenarioTagMatch, Does.Contain("CHARINDEX(',' + @ScenarioTag + ','"), "comma-delimited, so 'smoke' never matches 'smoke2'");
            Assert.That(sql, Does.Not.Contain("LIKE"), "'_' in a tag must not be a wildcard");
            Assert.That(sql, Does.Contain("ORDER BY row_creationdate, Scenario_id;"));
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
            Assert.That(sql, Does.Contain("response.row_creationdate AS CreatedDate"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Contain("response.Last_updated AS LastUpdated"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Contain("response.Scenario_id = @ScenarioId"));
        });
    }

    [Test]
    public void Passing_result_update_should_preserve_creation_date_and_update_status_response_and_timestamp()
    {
        var sql = ReadQuery<ResultUpdater>("UpdatePassingResultSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE [Xml].[Response]"));
            Assert.That(sql, Does.Contain("Status = @Status"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("XML_Response = @XmlResponse"));
            Assert.That(sql, Does.Contain("Last_updated = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("row_creationdate"), "an update never changes who or when the row was created");
            Assert.That(sql, Does.Not.Contain("row_createdby"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Quote_ref"));
            Assert.That(sql, Does.Not.Contain("Product_code"));
            Assert.That(sql, Does.Not.Contain("Scheme_code"));
        });
    }

    [Test]
    public void Failed_result_update_should_change_only_the_status()
    {
        var sql = ReadQuery<ResultUpdater>("UpdateFailedStatusSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("UPDATE [Xml].[Response]"));
            Assert.That(sql, Does.Contain("SET Status = @Status"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("Scenario_id = @ScenarioId"));
            Assert.That(sql, Does.Not.Contain("XML_Response"));
            Assert.That(sql, Does.Not.Contain("Last_updated"));
            Assert.That(sql, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("row_creationdate"));
        });
    }

    [Test]
    public void Baseline_insert_should_never_replace_an_existing_baseline()
    {
        var sql = ReadQuery<ResultUpdater>("InsertBaselineSql");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("(Scenario_id, XML_Response, Status, Last_updated,"));
            Assert.That(sql, Does.Contain("row_creationdate, row_createdby, row_modifieddate, row_modifiedby)"));
            Assert.That(sql, Does.Contain("COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("Build_id"));
            Assert.That(sql, Does.Not.Contain("@BuildId"));
            Assert.That(sql, Does.Contain("SELECT @ScenarioId, @XmlResponse, @Status, SYSUTCDATETIME(),"));
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
            Assert.That(sql, Does.Contain("INSERT INTO [Xml].[Request] (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags, row_creationdate, row_createdby, row_modifieddate, row_modifiedby)"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME()), SYSUTCDATETIME(), COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Contain("SYSUTCDATETIME()"));
            Assert.That(sql, Does.Contain("(@s0, @q0, @p0, @c0, @x0, @t0)"));
            Assert.That(sql, Does.Contain("(@s1, @q1, @p1, @c1, @x1, @t1)"));
            Assert.That(sql, Does.Contain("AS v (Scenario_id, Quote_ref, Product_code, Scheme_code, XML_request, Test_tags)"));
            Assert.That(sql, Does.Contain("WHERE NOT EXISTS"));
            Assert.That(sql, Does.Contain("[Xml].[Request] AS target WITH (UPDLOCK, HOLDLOCK)"));
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
            Assert.That(sql, Does.Not.Contain("[Xml].[Response]"));
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

    [Test]
    public void Import_updates_should_use_values_and_preserve_identity_tags_creation_and_baselines()
    {
        var sql = ScenarioImportRepository.BuildUpdateSql(2);
        var assignments = sql.Split("SET", StringSplitOptions.None)[1].Split("FROM", StringSplitOptions.None)[0];

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("FROM [Xml].[Request] AS target WITH (UPDLOCK, HOLDLOCK)"));
            Assert.That(sql, Does.Contain("(@s1, @q1, @p1, @c1, @x1, @t1)"));
            Assert.That(sql, Does.Contain("ON target.Scenario_id = v.Scenario_id"));
            Assert.That(sql, Does.Not.Contain("target.Product_code"), "no product/scheme condition: the XML alone decides");
            Assert.That(sql, Does.Not.Contain("target.Scheme_code"));
            Assert.That(sql, Does.Not.Contain("target.Quote_ref"));
            Assert.That(assignments, Does.Contain("XML_request = v.XML_request"));
            Assert.That(assignments, Does.Contain("row_modifieddate = SYSUTCDATETIME()"));
            Assert.That(assignments, Does.Contain("row_modifiedby = COALESCE(@AuditUser, SUSER_SNAME())"));
            Assert.That(assignments, Does.Not.Contain("row_creationdate"));
            Assert.That(assignments, Does.Not.Contain("row_createdby"));
            Assert.That(assignments, Does.Not.Contain("Quote_ref"), "the update changes the XML only");
            Assert.That(assignments, Does.Not.Contain("Test_tags"));
            Assert.That(assignments, Does.Not.Contain("Product_code"));
            Assert.That(assignments, Does.Not.Contain("Scheme_code"));
            Assert.That(sql, Does.Not.Contain("Create_date"));
            Assert.That(sql, Does.Not.Contain("[Xml].[Response]"));
            Assert.That(sql, Does.Not.Contain("#"));
            Assert.That(sql, Does.Not.Match(@"\bCREATE\s").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("MERGE").IgnoreCase);
            Assert.That(sql, Does.Not.Contain("OPENJSON").IgnoreCase);
        });
    }

    [TestCase("testuser", "testuser")]
    [TestCase("  testuser  ", "testuser")]
    [TestCase("", null)]
    [TestCase("   ", null)]
    public void Audit_user_should_be_trimmed_or_left_null_for_the_sql_login(string configured, string? expected)
    {
        var factory = new SqlConnectionFactory(new DatabaseSettings { AuditUser = configured });
        ScenarioRequestImport[] rows = [new("S1", "Q1", "HOME", "ABC", "<Message />", "")];
        var parameters = ScenarioImportRepository.BuildParameters(rows, factory.AuditUser);

        Assert.Multiple(() =>
        {
            Assert.That(factory.AuditUser, Is.EqualTo(expected));
            Assert.That(parameters.ParameterNames, Does.Contain("AuditUser"));
            Assert.That(parameters.Get<string?>("AuditUser"), Is.EqualTo(expected));
        });
    }

    [Test]
    public void Import_parameters_should_bind_large_xml_as_varchar_max()
    {
        var xml = "<Message>" + new string('x', 46000) + "</Message>";
        var parameters = ScenarioImportRepository.BuildParameters([new("S1", "Q1", "HOME", "ABC", xml, "")]);
        using var connection = new SqlConnectionFactory(new DatabaseSettings()).Create();
        using var command = connection.CreateCommand();
        var identity = (Dapper.SqlMapper.Identity)Activator.CreateInstance(
            typeof(Dapper.SqlMapper.Identity), BindingFlags.Instance | BindingFlags.NonPublic, null,
            [ScenarioImportRepository.BuildUpdateSql(1), global::System.Data.CommandType.Text, connection, null, parameters.GetType()], null)!;
        ((Dapper.SqlMapper.IDynamicParameters)parameters).AddParameters(command, identity);
        var parameter = command.Parameters["x0"];

        Assert.Multiple(() =>
        {
            Assert.That(parameter.SqlDbType, Is.EqualTo(global::System.Data.SqlDbType.VarChar));
            Assert.That(parameter.Size, Is.EqualTo(-1));
            Assert.That(parameter.Value, Is.EqualTo(xml));
        });
    }

    [Test]
    public void Import_update_should_stay_within_sql_servers_parameter_limit()
    {
        var sql = ScenarioImportRepository.BuildUpdateSql(ScenarioImportRepository.MaxRowsPerStatement);
        var parameters = global::System.Text.RegularExpressions.Regex.Matches(sql, @"@[a-z]\d+")
            .Select(match => match.Value).Distinct().Count();

        Assert.That(parameters, Is.EqualTo(1998));
    }

    [TestCase(0)]
    [TestCase(334)]
    public void Import_update_should_reject_statement_sizes_outside_the_limit(int rowCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioImportRepository.BuildUpdateSql(rowCount));
    }

    private static string ReadQuery<TReader>(string fieldName)
    {
        var field = typeof(TReader).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetRawConstantValue() as string
            ?? throw new AssertionException($"SQL constant '{typeof(TReader).Name}.{fieldName}' was not found.");
    }
}