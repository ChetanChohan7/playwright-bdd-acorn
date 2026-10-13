namespace PricingValidationFramework.Core.Models.Common;

public class PipelineSettings
{
    public string BuildId { get; set; } = string.Empty;
    /// Product filter (TEST_TAG): an exact [Xml].[Request].Product_code; empty selects every product.
    public string TestTag { get; set; } = string.Empty;

    /// Tag filter (SCENARIO_TAG): one tag from [Xml].[Request].Test_tags; empty selects every scenario.
    public string ScenarioTag { get; set; } = string.Empty;
    public string RequestTime { get; set; } = string.Empty;
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }
}
