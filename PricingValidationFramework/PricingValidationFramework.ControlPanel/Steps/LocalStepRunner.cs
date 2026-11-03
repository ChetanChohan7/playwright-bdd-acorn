namespace PricingValidationFramework.ControlPanel.Steps;

using System.Collections.Concurrent;
using System.Diagnostics;

/// Runs each step as a command on this machine (the DataLoader CLI or dotnet test), one at a time,
/// capturing its output for the page.
public sealed class LocalStepRunner(ControlPanelPaths paths, ControlPanelState state) : IStepRunner, IDisposable
{
    private readonly object gate = new();
    private readonly ConcurrentDictionary<string, StepJob> jobs = new(StringComparer.Ordinal);
    private StepJob? current;
    private Process? currentProcess;

    public StepJob? Current
    {
        get { lock (gate) { return current?.Status == StepStatus.Running ? current : null; } }
    }

    public StepJob Start(StepRequest request)
    {
        lock (gate)
        {
            if (current?.Status == StepStatus.Running)
            {
                throw new StepRunnerBusyException(current);
            }

            // e.g. local-20261021-093000-123: unique per run, and safe in file names and as a build ID.
            var runId = $"local-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
            var spec = LocalCommandBuilder.Build(request, paths, runId);
            var job = new StepJob(runId, request.Kind) { Outputs = spec.Outputs };
            jobs[job.Id] = job;
            current = job;
            _ = Task.Run(() => RunAsync(job, spec));
            return job;
        }
    }

    public StepJob? Find(string id) => jobs.TryGetValue(id, out var job) ? job : null;

    public bool CancelCurrent()
    {
        lock (gate)
        {
            if (current?.Status != StepStatus.Running || currentProcess is null)
            {
                return false;
            }

            try
            {
                currentProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            current.AppendLine("Cancelled from the control panel.");
            current.Finish(StepStatus.Cancelled, null, ["Cancelled. Anything already committed to the database stays; check the summary before running it again."]);
            return true;
        }
    }

    private async Task RunAsync(StepJob job, ProcessSpec spec)
    {
        var output = new ConcurrentQueue<string>();
        try
        {
            Directory.CreateDirectory(paths.WorkFolder);
            var startInfo = new ProcessStartInfo(spec.FileName)
            {
                WorkingDirectory = spec.WorkingFolder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Arguments are passed as a list, never through a shell, so typed values can't run as commands.
            foreach (var argument in spec.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in spec.Environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(name);
                }
                else
                {
                    startInfo.Environment[name] = value;
                }
            }

            job.AppendLine($"> {spec.FileName} {string.Join(' ', spec.Arguments.Select(Quote))}");
            using var process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.Enqueue(e.Data); job.AppendLine(e.Data); } };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { output.Enqueue(e.Data); job.AppendLine(e.Data); } };

            lock (gate)
            {
                process.Start();
                currentProcess = process;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            lock (gate)
            {
                currentProcess = null;
                if (job.Status == StepStatus.Cancelled)
                {
                    return;
                }
            }

            if (job.Kind == StepKind.Radar && spec.Outputs.FirstOrDefault() is { } report && File.Exists(report.Path))
            {
                state.LastRadarReport = report.Path;
            }

            var summary = StepResultReader.Summarize(job.Kind, spec, output.ToArray());
            job.Finish(process.ExitCode == 0 ? StepStatus.Succeeded : StepStatus.Failed, process.ExitCode, summary);
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                currentProcess = null;
            }

            job.AppendLine(exception.ToString());
            job.Finish(StepStatus.Failed, null, [$"The step couldn't run ({exception.GetType().Name}): {exception.Message}"]);
        }
    }

    private static string Quote(string argument) => argument.Contains(' ') ? $"\"{argument}\"" : argument;

    public void Dispose() => CancelCurrent();
}
