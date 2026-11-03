namespace PricingValidationFramework.ControlPanel.Steps;

using PricingValidationFramework.Core.Models.Enums;

public enum StepKind
{
    ValidateCsv,
    Import,
    Seed,
    Radar,
    Ice,
    SetStatus
}

/// What a step should do, independent of how it runs (locally now, a pipeline later). Values are already
/// checked by StepInputs; null means "not given", so the settings file or the command's default applies.
public abstract record StepRequest(StepKind Kind);

/// 1. Check a chosen scenarios CSV offline (no database).
public sealed record ValidateCsvRequest(string CsvPath) : StepRequest(StepKind.ValidateCsv);

/// 2. Import the chosen CSV into [Xml].[Request].
public sealed record ImportRequest(string CsvPath, string? RequestTime, int? BatchSize) : StepRequest(StepKind.Import);

/// 3. Seed first baselines into [Xml].[Response] for scenarios that have none.
public sealed record SeedRequest(string? RequestTime, int? MaxParallel) : StepRequest(StepKind.Seed);

/// 4. Radar compare: the Radar tests, with the pipeline's inputs.
public sealed record RadarRequest(
    ThresholdType? ThresholdType,
    decimal? MinThreshold,
    decimal? MaxThreshold,
    string? ProductCode,
    string? ScenarioTag,
    string? RequestTime) : StepRequest(StepKind.Radar);

/// 5. ICE check: the ICE tests. With RadarReport, exactly the scenarios that passed in that Radar run;
/// otherwise the newest PASS baseline per product/scheme within the filters.
public sealed record IceRequest(string? ProductCode, string? ScenarioTag, string? RadarReport) : StepRequest(StepKind.Ice);

/// 6. Set [Xml].[Response].Status to PASS or FAIL for the listed scenario IDs and/or a file of IDs.
public sealed record SetStatusRequest(string Status, string? ScenarioIds, string? ScenarioIdsFile) : StepRequest(StepKind.SetStatus);
