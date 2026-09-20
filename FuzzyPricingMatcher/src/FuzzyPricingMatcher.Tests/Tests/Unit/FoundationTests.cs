namespace FuzzyPricingMatcher.Tests.Tests.Unit;

using FuzzyPricingMatcher.Tests.Configuration;
using NUnit.Framework;

public sealed class FoundationTests
{
    [Test]
    [Category("Unit")]
    public void Foundation_is_ready_for_future_automation_tests()
    {
        Assert.That(TestContext.CurrentContext, Is.Not.Null);
    }

    [Test]
    [Category("Unit")]
    public void Configuration_has_required_safe_placeholders()
    {
        var configuration = MatcherConfiguration.Load(TestContext.CurrentContext.TestDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(configuration.Routes, Has.Count.EqualTo(17));
            Assert.That(configuration.Endpoints, Has.Count.EqualTo(3));
            Assert.That(configuration.Database.RequestTableName, Is.EqualTo("dbo.tb_xmlrequest"));
            Assert.That(configuration.Database.ResponseTableName, Is.EqualTo("dbo.tb_response"));
            Assert.That(configuration.Automation.MinimumThreshold, Is.EqualTo(-0.05m));
            Assert.That(configuration.Automation.MaximumThreshold, Is.EqualTo(0.05m));
            Assert.That(configuration.Automation.TagMatchMode, Is.EqualTo(TagMatchMode.Any));
            Assert.That(configuration.Loader.CsvPath, Is.EqualTo("TestAsset/baseline-scenarios.csv"));
            Assert.That(configuration.Resilience.ParallelWorkers, Is.EqualTo(4));
            Assert.That(configuration.Routes["TODO_SCHEME_01"].EndpointName, Is.EqualTo("EndpointA"));
            Assert.That(configuration.Routes["TODO_SCHEME_07"].EndpointName, Is.EqualTo("EndpointB"));
            Assert.That(configuration.Routes["TODO_SCHEME_13"].EndpointName, Is.EqualTo("EndpointC"));
            Assert.That(configuration.Routes.Values, Is.All.Matches<RouteSettings>(route => !route.Enabled));
        });
    }

    [Test]
    [Category("Unit")]
    public void Environment_variables_override_json_values()
    {
        const string variableName = "Pipeline__BuildId";
        var originalValue = Environment.GetEnvironmentVariable(variableName);
        try
        {
            Environment.SetEnvironmentVariable(variableName, "FROM_ENVIRONMENT");
            var configuration = MatcherConfiguration.Load(TestContext.CurrentContext.TestDirectory);
            Assert.That(configuration.Pipeline.BuildId, Is.EqualTo("FROM_ENVIRONMENT"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Test]
    [Category("Unit")]
    public void Disabled_or_unknown_routes_are_rejected_without_http_calls()
    {
        var configuration = MatcherConfiguration.Load(TestContext.CurrentContext.TestDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(() => configuration.GetEnabledRoute("TODO_SCHEME_01"), Throws.TypeOf<ConfigurationValidationException>());
            Assert.That(() => configuration.GetEnabledRoute("UNKNOWN_SCHEME"), Throws.TypeOf<ConfigurationValidationException>());
            Assert.That(() => configuration.GetEnabledEndpoint("EndpointA"), Throws.TypeOf<ConfigurationValidationException>());
        });
    }

    [Test]
    [Category("Unit")]
    public void Committed_configuration_examples_are_inert_and_non_secret()
    {
        var baseConfiguration = MatcherConfiguration.LoadStandalone(Path.Combine(TestContext.CurrentContext.TestDirectory, "appsettings.json"));
        var exampleConfiguration = MatcherConfiguration.LoadStandalone(Path.Combine(TestContext.CurrentContext.TestDirectory, "appsettings.Local.example.json"));
        Assert.Multiple(() =>
        {
            Assert.That(baseConfiguration.Endpoints.Values, Is.All.Matches<EndpointSettings>(endpoint => !endpoint.Enabled));
            Assert.That(exampleConfiguration.Endpoints.Values, Is.All.Matches<EndpointSettings>(endpoint => !endpoint.Enabled));
            Assert.That(baseConfiguration.Routes.Values, Is.All.Matches<RouteSettings>(route => !route.Enabled));
            Assert.That(exampleConfiguration.Routes.Values, Is.All.Matches<RouteSettings>(route => !route.Enabled));
            Assert.That(exampleConfiguration.Database.SqlConnectionString, Does.Contain("__"));
            Assert.That(exampleConfiguration.Endpoints.Values.Select(endpoint => endpoint.Password), Is.All.Matches<string>(password => string.IsNullOrEmpty(password) || password.Contains("__", StringComparison.Ordinal)));
            Assert.That(baseConfiguration.Endpoints.Values.Select(endpoint => endpoint.BaseUrl), Is.All.Matches<string>(url => url.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)));
            Assert.That(exampleConfiguration.Endpoints.Values.Select(endpoint => endpoint.BaseUrl), Is.All.Matches<string>(url => url.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)));
        });
    }
}