namespace PricingXml.ScenarioTool;

/// <summary>
/// One declarative field edit: set &lt;ElementName&gt; under ParentXPath to Value,
/// optionally only on rows where ConditionXPath's element text equals ConditionValue.
/// </summary>
public sealed record FieldEdit(
    string ParentXPath,
    string ElementName,
    string Value,
    string? ConditionXPath = null,
    string? ConditionValue = null,
    bool AddIfMissing = false);
