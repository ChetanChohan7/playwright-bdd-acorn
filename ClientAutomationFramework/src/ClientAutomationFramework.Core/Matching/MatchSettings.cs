namespace ClientAutomationFramework.Core.Matching;

public sealed class MatchSettings
{
    public decimal MinimumThreshold { get; set; } = -0.02m;
    public decimal MaximumThreshold { get; set; } = 0.02m;
}
