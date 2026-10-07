namespace PricingValidationFramework.DataLoader;

using System.Text.RegularExpressions;

/// Turns the --requests argument into the CSV to load. A file is used as-is. For a folder, the
/// newest scenarios-{yyyyMMdd-HHmmss-fff}.csv saved by the CSV loader UI is chosen by the
/// timestamp in its name (not the file's modified time, which changes on copy or checkout),
/// falling back to a plain requests.csv when the folder has no versioned files.
internal static partial class RequestFileResolver
{
    public const string FallbackFileName = "requests.csv";

    [GeneratedRegex(@"^scenarios-(\d{8}-\d{6}-\d{3})\.csv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionedFileName();

    public static string Resolve(string path)
    {
        if (File.Exists(path))
        {
            return path;
        }

        if (!Directory.Exists(path))
        {
            throw new InvalidDataException($"The request CSV file or folder '{path}' was not found.");
        }

        // yyyyMMdd-HHmmss-fff sorts chronologically as plain text.
        var newest = Directory.EnumerateFiles(path, "*.csv")
            .Select(file => (File: file, Match: VersionedFileName().Match(Path.GetFileName(file))))
            .Where(candidate => candidate.Match.Success)
            .OrderByDescending(candidate => candidate.Match.Groups[1].Value, StringComparer.Ordinal)
            .Select(candidate => candidate.File)
            .FirstOrDefault();
        if (newest is not null)
        {
            return newest;
        }

        var fallback = Path.Combine(path, FallbackFileName);
        return File.Exists(fallback)
            ? fallback
            : throw new InvalidDataException(
                $"No scenarios-{{timestamp}}.csv or {FallbackFileName} was found in the folder '{path}'.");
    }
}
