using ClientAutomationFramework.Core.Matching;
using NLog;

namespace ClientAutomationFramework.Core.Logging;

public sealed class TestRunLogger
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Outcome(MatchResult result) =>
        Logger.Info("ScenarioId={ScenarioId} QuoteRef={QuoteRef} Status={Status} Detail={Detail}", result.ScenarioId, result.QuoteRef, result.Status, result.Detail);

    public void Error(string scenarioId, string message) =>
        Logger.Error("ScenarioId={ScenarioId} Error={Message}", scenarioId, message);
}
