namespace PricingValidationFramework.Core.Models.Common;

public class PipelineSettings
{
    public string BuildId { get; set; } = string.Empty;
    public string TestTag { get; set; } = string.Empty;
    public string RequestTime { get; set; } = string.Empty;
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }

    /// When true, Radar only creates baselines for scenarios that have none yet; scenarios with an
    /// existing [Xml].[Response] row are skipped without calling Radar. Used after a data load.
    public bool BaselineOnly { get; set; }
}
