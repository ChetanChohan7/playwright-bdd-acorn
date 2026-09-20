namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class AutomationSettings
{
    public decimal MinimumThreshold { get; set; } = -0.05m;
    public decimal MaximumThreshold { get; set; } = 0.05m;
    public TagMatchMode TagMatchMode { get; set; } = TagMatchMode.Any;
}