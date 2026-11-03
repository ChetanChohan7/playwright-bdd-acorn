namespace PricingValidationFramework.ControlPanel;

/// The "ControlPanel" section of appsettings.json.
public sealed class ControlPanelOptions
{
    public int Port { get; set; } = 5080;

    /// The folder holding PricingValidationFramework.slnx. Empty: found by walking up from this app's folder.
    public string SolutionFolder { get; set; } = string.Empty;

    /// The settings file every step uses (database, Radar, ICE, thresholds), relative to SolutionFolder.
    public string LocalSettings { get; set; } = "data/import/local-settings.json";

    /// Build configuration the DataLoader and tests run in (Debug matches the VS Code launch configurations).
    public string Configuration { get; set; } = "Debug";

    public int MaxUploadMegabytes { get; set; } = 200;

    /// The absolute solution folder: the configured one, or the nearest parent of startFolder with the .slnx.
    public string ResolveSolutionFolder(string startFolder)
    {
        if (!string.IsNullOrWhiteSpace(SolutionFolder))
        {
            return Path.GetFullPath(SolutionFolder);
        }

        for (var folder = new DirectoryInfo(startFolder); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "PricingValidationFramework.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            "PricingValidationFramework.slnx wasn't found above this app. Set ControlPanel:SolutionFolder in appsettings.json.");
    }
}
