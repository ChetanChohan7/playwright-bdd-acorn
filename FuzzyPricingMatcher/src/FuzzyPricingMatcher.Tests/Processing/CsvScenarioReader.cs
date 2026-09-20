using CsvHelper;
using CsvHelper.Configuration;
using FuzzyPricingMatcher.Tests.Models;
using FuzzyPricingMatcher.Tests.Validation;
using System.Globalization;

namespace FuzzyPricingMatcher.Tests.Processing;

public sealed class CsvScenarioReader : ICsvScenarioReader
{
    private static readonly string[] RequiredHeaders = ["Scenario_id", "XML_request", "Test_tags"];
    private readonly IScenarioIdNormalizer scenarioIdNormalizer;
    private readonly IRequestXmlMetadataReader metadataReader;
    private readonly ITagNormalizer tagNormalizer;
    private readonly IXmlFingerprintService fingerprintService;

    public CsvScenarioReader(IScenarioIdNormalizer? scenarioIdNormalizer = null, IRequestXmlMetadataReader? metadataReader = null, ITagNormalizer? tagNormalizer = null, IXmlFingerprintService? fingerprintService = null)
    {
        this.scenarioIdNormalizer = scenarioIdNormalizer ?? new ScenarioIdNormalizer();
        this.metadataReader = metadataReader ?? new RequestXmlMetadataReader();
        this.tagNormalizer = tagNormalizer ?? new TagNormalizer();
        this.fingerprintService = fingerprintService ?? new XmlFingerprintService();
    }

    public IReadOnlyList<PreparedBaselineScenario> Read(string path, bool requireDataRows = false)
    {
        if (!File.Exists(path))
            throw new CsvValidationException($"CSV file '{path}' does not exist.");

        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null, HeaderValidated = null });
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            throw new CsvValidationException("CSV must contain a header row.");

        var headerIndexes = csv.HeaderRecord.Select((header, index) => new { Header = header.Trim(), Index = index }).ToDictionary(item => item.Header, item => item.Index, StringComparer.OrdinalIgnoreCase);
        foreach (var requiredHeader in RequiredHeaders)
            if (!headerIndexes.ContainsKey(requiredHeader))
                throw new CsvValidationException($"CSV is missing required header '{requiredHeader}'.");

        var results = new List<PreparedBaselineScenario>();
        while (csv.Read())
        {
            var rowNumber = csv.Parser.Row;
            var csvRow = new BaselineScenarioCsvRow
            {
                RowNumber = rowNumber,
                ScenarioId = csv.GetField(headerIndexes["Scenario_id"]) ?? string.Empty,
                XmlRequest = csv.GetField(headerIndexes["XML_request"]) ?? string.Empty,
                TestTags = csv.GetField(headerIndexes["Test_tags"]) ?? string.Empty
            };
            string normalizedId;
            try
            {
                normalizedId = scenarioIdNormalizer.Normalize(csvRow.ScenarioId);
            }
            catch (CsvValidationException exception)
            {
                throw new CsvValidationException($"{exception.Message} at CSV row {rowNumber}.");
            }
            if (string.IsNullOrWhiteSpace(csvRow.XmlRequest))
                throw new CsvValidationException($"XML_request is required at CSV row {rowNumber}.");
            var requestMetadata = metadataReader.Read(csvRow.XmlRequest);
            results.Add(new PreparedBaselineScenario { CsvRow = csvRow, NormalizedScenarioId = normalizedId, RequestMetadata = requestMetadata, NormalizedTags = tagNormalizer.Normalize(csvRow.TestTags), XmlFingerprint = fingerprintService.CreateFingerprint(requestMetadata.Document) });
        }

        var duplicates = new DuplicateScenarioDetector(scenarioIdNormalizer).Detect(results.Select(result => result.CsvRow));
        if (duplicates.Count > 0)
            throw new CsvValidationException(string.Join(" ", duplicates.Select(duplicate => duplicate.ErrorMessage + $" Rows: {string.Join(", ", duplicate.RowNumbers)}.")));
        if (requireDataRows && results.Count == 0)
            throw new CsvValidationException("CSV contains no data rows.");
        return results;
    }
}