namespace PricingValidationFramework.Core.Models.Common;

public class PipelineSettings
{
    public string BuildId { get; set; } = string.Empty;
    public string TestTag { get; set; } = string.Empty;
    public string RequestTime { get; set; } = string.Empty;
    public decimal MinThreshold { get; set; }
    public decimal MaxThreshold { get; set; }
}

// pipeseeting to radar pipeline settings 
