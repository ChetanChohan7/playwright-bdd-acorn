using PricingValidationFramework.Core.Models.Database;

namespace PricingValidationFramework.Tests.Helpers.Validation;

public static class RadarScenarioTestCases
{
	public static IReadOnlyList<TestCaseData> Create(IEnumerable<ScenarioRequest> scenarios)
	{
		ArgumentNullException.ThrowIfNull(scenarios);
		var selectedScenarios = scenarios.ToArray();

		if (selectedScenarios.Length == 0)
		{
			return [DiscoveryFailure("No Radar scenarios were returned for the selected workload.")];
		}

		if (selectedScenarios
			.GroupBy(scenario => scenario.ScenarioId, StringComparer.OrdinalIgnoreCase)
			.Any(group => group.Count() > 1))
		{
			return [DiscoveryFailure("Duplicate ScenarioId values were returned for the selected workload.")];
		}

		return selectedScenarios.Select(CreateScenarioCase).ToArray();
	}

	public static TestCaseData DiscoveryFailure(string safeReason)
	{
		return new TestCaseData(null, safeReason)
			.SetName("Radar_scenario_discovery_should_succeed")
			.SetCategory("RadarDiscovery");
	}

	private static TestCaseData CreateScenarioCase(ScenarioRequest scenario)
	{
		var testCase = new TestCaseData(scenario, null)
			.SetName($"Radar_scenario_{SanitizeScenarioId(scenario.ScenarioId)}")
			.SetCategory("Radar");

		foreach (var category in GetCategories(scenario.TestTags))
		{
			testCase.SetCategory(category);
		}

		return testCase;
	}

	private static IEnumerable<string> GetCategories(string? testTags)
	{
		return (testTags ?? string.Empty)
			.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(tag => !string.IsNullOrWhiteSpace(tag))
			.Distinct(StringComparer.Ordinal);
	}

	private static string SanitizeScenarioId(string? scenarioId)
	{
		if (string.IsNullOrWhiteSpace(scenarioId))
		{
			return "unknown";
		}

		var safeId = new string(scenarioId
			.Take(64)
			.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_')
			.ToArray());

		return string.IsNullOrWhiteSpace(safeId) ? "unknown" : safeId;
	}
}