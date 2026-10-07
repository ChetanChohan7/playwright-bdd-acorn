using ClientAutomationFramework.Core.ExternalAPIAccess;
using ClientAutomationFramework.Core.Extraction;
using ClientAutomationFramework.Core.Matching;
using ClientAutomationFramework.Tests.TestBase;

namespace ClientAutomationFramework.Tests.Integration.Json;

[TestFixture]
public sealed class JsonComparisonTests
{
    [Test]
    [Category("Integration")]
    public async Task Quote_pricing_component_matches_the_stored_xml_response()
    {
        var scenarioId = TestContext.Parameters.Get("ScenarioId", string.Empty);
        Assert.That(scenarioId, Is.Not.Empty, "Pass -TestParams:ScenarioId=... to run this test.");

        // TEMPORARY: reads data/xml-response.json instead of TestSetup.ResponseReader while the
        // real database is still being set up. Swap back to
        // `await TestSetup.ResponseReader.GetLastResponseAsync(scenarioId)` once it's ready -
        // see TestBase/JsonFileResponseSource.cs.
        var last = JsonFileResponseSource.GetLast(TestSetup.StoredResponsesPath, scenarioId);
        Assert.That(last, Is.Not.Null, $"No stored response found for Scenario_id '{scenarioId}' in {TestSetup.StoredResponsesPath}; run the XML tolerance test first.");
        Assert.That(last!.PassFail, Is.EqualTo("PASS"), $"Latest stored response has pass_fail='{last.PassFail}', not PASS.");

        var storedAmount = XmlValueExtractor.ExtractPremium(last.XmlResponse);
        Assert.That(storedAmount, Is.Not.Null, "Could not re-extract the stored premium amount.");

        var apiClient = new JsonApiClient(TestSetup.Client, TestSetup.Configuration.JsonScheme);
        var apiResult = await apiClient.GetAsync(last.QuoteRef);
        Assert.That(apiResult.Successful, Is.True, $"GET quote failed: {apiResult.Error}");

        var quoteAmount = JsonValueExtractor.ExtractPricingComponentAmount(apiResult.Body);
        Assert.That(quoteAmount, Is.Not.Null, $"Could not extract underwrittenDataPoints[0].technicalPrice.grossPremiumAmountIncTax from the response. Raw content: {apiResult.Body}");

        var matcher = new JsonExactMatcher();
        var result = matcher.Evaluate(scenarioId, last.QuoteRef, quoteAmount, storedAmount);
        TestSetup.Logger.Outcome(result);

        Assert.That(result.Passed, Is.True, result.Detail);
    }
}
