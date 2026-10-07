namespace PricingValidationFramework.Tests.Helpers.Reporting;

/// Shared between IceReportingHelper and RadarReportingHelper: write to a sibling temp file
/// first, then move it into place, so a run that fails partway through never leaves a
/// truncated/partial CSV at the real report path.
internal static class AtomicReportWriter
{
    public static async Task WriteAsync(string reportPath, Func<string, Task> writeToPathAsync)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(reportPath)!,
            $".{Path.GetFileName(reportPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await writeToPathAsync(temporaryPath);
            File.Move(temporaryPath, reportPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
            }

            throw;
        }
    }
}
