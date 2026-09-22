using ClientAutomationFramework.Core.ExternalAPIAccess;
using ClientAutomationFramework.Core.Extraction;
using ClientAutomationFramework.Core.Matching;
using ClientAutomationFramework.Core.Models;

namespace ClientAutomationFramework.Tests.Integration.Xml;

[TestFixture]
public sealed class XmlToleranceTests
{
    private static IEnumerable<ScenarioRequest> Scenarios() =>
        TestSetup.RequestReader.GetRequestsAsync(TestContext.Parameters.Get("ScenarioId")).GetAwaiter().GetResult();

    [TestCaseSource(nameof(Scenarios))]
    [Category("Integration")]
    public async Task Response_premium_is_within_tolerance_of_the_last_stored_response(ScenarioRequest scenario)
    {
        var apiClient = new XmlApiClient(TestSetup.Client, TestSetup.Configuration.XmlScheme);
        var matcher = new XmlToleranceMatcher(TestSetup.Configuration.Match);

        // Read the baseline before it is superseded below - it is the previous response for
        // this scenario_id, not a separately configured value.
        var baseline = await TestSetup.ResponseReader.GetLastResponseAsync(scenario.ScenarioId);
        var baselineAmount = baseline is null ? null : XmlValueExtractor.ExtractPremium(baseline.ResponseBody);

        var apiResult = await apiClient.SendAsync(scenario.RequestBody);
        Assert.That(apiResult.Successful, Is.True, $"API call failed: {apiResult.Error}");

        var currentAmount = XmlValueExtractor.ExtractPremium(apiResult.Body);
        var result = matcher.Evaluate(scenario.ScenarioId, scenario.QuoteRef, currentAmount, baselineAmount);

        await TestSetup.ResultUpdater.InsertAsync(new NewScenarioResponse(scenario.ScenarioId, scenario.QuoteRef, apiResult.Body, result.Status));
        TestSetup.Logger.Outcome(result);

        Assert.That(result.Passed, Is.True, result.Detail);
    }
}
