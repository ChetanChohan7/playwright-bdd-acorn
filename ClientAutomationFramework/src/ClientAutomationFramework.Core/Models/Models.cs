namespace ClientAutomationFramework.Core.Models;

public sealed record ScenarioRequest(string ScenarioId, string QuoteRef, string RequestBody);

public sealed record ScenarioResponse(string ScenarioId, string QuoteRef, string ResponseBody, string Status, DateTime CreatedDate);

public sealed record NewScenarioResponse(string ScenarioId, string QuoteRef, string ResponseBody, string Status);
