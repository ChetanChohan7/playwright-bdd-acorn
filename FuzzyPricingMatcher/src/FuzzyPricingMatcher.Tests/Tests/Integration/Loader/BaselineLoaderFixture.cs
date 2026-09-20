using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Infrastructure;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Processing;
using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Integration.Loader;

[TestFixture]
[Category("LoadBaseline")]
[NonParallelizable]
public sealed class BaselineLoaderFixture
{
    [Test]
    public void Synchronizes_the_authoritative_csv_through_production_adapters()
    {
        if (!IntegrationTestExecution.IsEnabled)
            Assert.Fail(IntegrationTestExecution.DisabledMessage);

        using var compositionRoot = AutomationCompositionRoot.Create(TestContext.CurrentContext.TestDirectory);
        var configuration = compositionRoot.GetRequiredService<MatcherConfiguration>();
        var scenarioReader = compositionRoot.GetRequiredService<CsvScenarioReader>();
        var solutionRoot = FindSolutionRoot();
        var csvPath = Path.GetFullPath(Path.Combine(solutionRoot, configuration.Loader.CsvPath));
        var scenarioRecords = scenarioReader.Read(csvPath, requireDataRows: true);
        compositionRoot.GetRequiredService<IntegrationConfigurationValidator>().ValidateLoader(csvPath, scenarioRecords);
        var synchronizationResult = compositionRoot.GetRequiredService<LoaderSynchronizationService>().Synchronize(scenarioRecords, configuration.Pipeline.BuildId);
        var summaryPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults", "loader-summary.txt");
        compositionRoot.GetRequiredService<ILoaderSummaryWriter>().Write(summaryPath, synchronizationResult);
        Assert.That(synchronizationResult.Successful, Is.True, synchronizationResult.DeletionSkippedReason);
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FuzzyPricingMatcher.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }
}
