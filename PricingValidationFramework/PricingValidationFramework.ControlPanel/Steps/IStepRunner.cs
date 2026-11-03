namespace PricingValidationFramework.ControlPanel.Steps;

/// Runs one step at a time. LocalStepRunner runs the commands on this machine; a pipeline runner can
/// implement the same interface later (queue the Azure DevOps pipeline with the request's values), and
/// the page and endpoints stay as they are.
public interface IStepRunner
{
    /// The step currently running, if any.
    StepJob? Current { get; }

    /// Starts a step. Throws StepRunnerBusyException while another step is running.
    StepJob Start(StepRequest request);

    StepJob? Find(string id);

    /// Stops the running step. False if nothing is running.
    bool CancelCurrent();
}

public sealed class StepRunnerBusyException(StepJob running)
    : Exception($"A step is already running ({running.Kind}). Wait for it to finish, or cancel it.");

/// What the page remembers between steps: the CSV chosen in step 1 and the latest Radar report.
public sealed class ControlPanelState
{
    private readonly object gate = new();
    private string? selectedCsv;
    private string? selectedCsvName;
    private string? lastRadarReport;

    public (string? Path, string? OriginalName) SelectedCsv
    {
        get { lock (gate) { return (selectedCsv, selectedCsvName); } }
    }

    public string? LastRadarReport
    {
        get { lock (gate) { return lastRadarReport; } }
        set { lock (gate) { lastRadarReport = value; } }
    }

    public void SelectCsv(string path, string originalName)
    {
        lock (gate)
        {
            selectedCsv = path;
            selectedCsvName = originalName;
        }
    }
}
