namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class PipelineRunContext
{
    public string BuildId { get; set; } = "LOCAL";
    public string BuildNumber { get; set; } = "LOCAL";
    public string ApiDate { get; set; } = string.Empty;
    public string RequestedTestTags { get; set; } = string.Empty;
}