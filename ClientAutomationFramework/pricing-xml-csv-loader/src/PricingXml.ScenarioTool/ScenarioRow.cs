using CsvHelper.Configuration.Attributes;

namespace PricingXml.ScenarioTool;

public sealed class ScenarioRow
{
    [Name("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;

    [Name("xml")]
    public string Xml { get; set; } = string.Empty;
}
