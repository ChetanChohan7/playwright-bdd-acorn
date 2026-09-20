namespace FuzzyPricingMatcher.Tests.Infrastructure;

public static class IntegrationTestExecution
{
    public static bool IsEnabled => string.Equals(Environment.GetEnvironmentVariable("FUZZY_RUN_INTEGRATION"), "true", StringComparison.OrdinalIgnoreCase);

    public const string DisabledMessage = "Set FUZZY_RUN_INTEGRATION=true and provide approved SQL/API/XSD/processor configuration to run integration categories.";
}