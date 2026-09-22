namespace PricingXml.ScenarioTool;

/// <summary>
/// One or more raw XML elements (Xml — e.g. several sibling &lt;book&gt; blocks, one after
/// another) appended as new children under ParentXPath, on every row, optionally only on
/// rows where ConditionXPath's element text equals ConditionValue. Always appends: existing
/// elements with the same tag name are left untouched, since a tag can legitimately repeat
/// and there's no single meaning of "already present" the way there is for a scalar field.
/// Mirrors the UI's "Repeated element (array)" edit mode in section 2.
/// </summary>
public sealed record ElementAdd(
    string ParentXPath,
    string Xml,
    string? ConditionXPath = null,
    string? ConditionValue = null);
