namespace PricingValidationFramework.ControlPanel.Steps;

using System.Text.Json.Serialization;

[JsonConverter(typeof(JsonStringEnumConverter<StepStatus>))]
public enum StepStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}

/// One run of a step: its progress, output and result. Safe to read while the step is running.
public sealed class StepJob(string id, StepKind kind)
{
    public const int MaxLogLines = 20000;

    private readonly object gate = new();
    private readonly List<string> log = [];
    private int droppedLines;

    public string Id { get; } = id;
    public StepKind Kind { get; } = kind;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public DateTimeOffset? FinishedAt { get; private set; }
    public StepStatus Status { get; private set; } = StepStatus.Running;
    public int? ExitCode { get; private set; }
    public IReadOnlyList<string> Summary { get; private set; } = [];
    public IReadOnlyList<StepOutputFile> Outputs { get; set; } = [];

    public void AppendLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (gate)
        {
            if (log.Count == MaxLogLines)
            {
                // Keep the latest output; very long runs drop their earliest lines.
                log.RemoveAt(0);
                droppedLines++;
            }

            log.Add(line);
        }
    }

    /// Lines from position `from` (counting every line ever written), and the position to ask for next.
    public (IReadOnlyList<string> Lines, int Next) ReadLines(int from)
    {
        lock (gate)
        {
            var start = Math.Max(0, from - droppedLines);
            return (log.Skip(start).ToArray(), droppedLines + log.Count);
        }
    }

    public void Finish(StepStatus status, int? exitCode, IReadOnlyList<string> summary)
    {
        lock (gate)
        {
            Status = status;
            ExitCode = exitCode;
            Summary = summary;
            FinishedAt = DateTimeOffset.Now;
        }
    }
}
