using FuzzyPricingMatcher.Tests.Api;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Data;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

[TestFixture]
[Category("Unit")]
public sealed class CapturedLoggingTests
{
    [Test]
    public async Task Captured_logs_keep_safe_diagnostics_and_exclude_secrets_and_xml()
    {
        var target = new MemoryTarget { Layout = "${level}|${message}|${exception:format=type}" };
        var previous = LogManager.Configuration;
        var configuration = new LoggingConfiguration();
        configuration.AddRuleForAllLevels(target, "FuzzyPricingMatcher.Tests.*");
        LogManager.Configuration = configuration;
        try
        {
            using (ScopeContext.PushProperty("BuildId", "BUILD"))
            using (ScopeContext.PushProperty("ScenarioId", "SCN"))
            using (ScopeContext.PushProperty("QuoteRef", "Q"))
            using (ScopeContext.PushProperty("SchemeCode", "S-1"))
            using (ScopeContext.PushProperty("EndpointName", "EndpointA"))
            {
                new NLogScenarioLogger().Outcome(new ComparisonResult("SCN", "Q", ComparisonOutcome.ApiFailed, false, null, null, null, "password=__API_PASSWORD_SECRET__ authorization=__AUTHORIZATION_SECRET__ connectionString=__CONNECTION_STRING_SECRET__ <response>__RESPONSE_XML_SECRET__</response>"));
                var apiPipeline = new ApiRetryPipeline(new ResilienceSettings { ApiRetryAttempts = 1, ApiRetryInitialDelaySeconds = 0, ApiRetryMaximumDelaySeconds = 0 }, new PermitCounter(), (_, _) => Task.CompletedTask);
                var apiAttempts = 0;
                await apiPipeline.ExecuteAsync("captured-api", (_, _) =>
                {
                    apiAttempts++;
                    return Task.FromResult(apiAttempts == 1 ? ApiCallResult.Failure(503, "transport", 1, TimeSpan.Zero) : ApiCallResult.Success(200, "ok", 2, TimeSpan.Zero));
                });
                var sqlRetry = new DatabaseRetryExecutor(new ResilienceSettings { SqlRetryAttempts = 2, SqlRetryInitialDelaySeconds = 0, SqlRetryMaximumDelaySeconds = 0 }, (_, _) => Task.CompletedTask);
                var sqlAttempts = 0;
                await sqlRetry.ExecuteAsync("captured-sql", _ =>
                {
                    sqlAttempts++;
                    if (sqlAttempts == 1) throw new TimeoutException();
                    return Task.CompletedTask;
                });
            }
            LogManager.Flush();

            var output = string.Join(Environment.NewLine, target.Logs);
            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Contain("ScenarioId=\"SCN\""));
                Assert.That(output, Does.Contain("QuoteRef=\"Q\""));
                Assert.That(output, Does.Contain("Outcome=ApiFailed"));
                Assert.That(output, Does.Contain("captured-api"));
                Assert.That(output, Does.Contain("captured-sql"));
                Assert.That(output, Does.Contain("TimeoutException"));
                Assert.That(output, Does.Not.Contain("__API_PASSWORD_SECRET__"));
                Assert.That(output, Does.Not.Contain("__SQL_PASSWORD_SECRET__"));
                Assert.That(output, Does.Not.Contain("__AUTHORIZATION_SECRET__"));
                Assert.That(output, Does.Not.Contain("__CONNECTION_STRING_SECRET__"));
                Assert.That(output, Does.Not.Contain("__REQUEST_XML_SECRET__"));
                Assert.That(output, Does.Not.Contain("__RESPONSE_XML_SECRET__"));
            });
        }
        finally
        {
            LogManager.Configuration = previous;
        }
    }

    private sealed class PermitCounter : IApiRateLimiter
    {
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
