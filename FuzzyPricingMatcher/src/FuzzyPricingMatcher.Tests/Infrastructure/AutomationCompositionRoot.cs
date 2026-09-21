using FuzzyPricingMatcher.Tests.ExternalAPIAccess;
using FuzzyPricingMatcher.Tests.Comparison;
using FuzzyPricingMatcher.Tests.Configuration;
using FuzzyPricingMatcher.Tests.Database;
using FuzzyPricingMatcher.Tests.Evidence;
using FuzzyPricingMatcher.Tests.Loader;
using FuzzyPricingMatcher.Tests.Processing;
using FuzzyPricingMatcher.Tests.Routing;
using FuzzyPricingMatcher.Tests.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FuzzyPricingMatcher.Tests.Infrastructure;

public sealed class AutomationCompositionRoot : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IReadOnlySet<Type> registeredTypes;

    private AutomationCompositionRoot(ServiceProvider provider, IReadOnlySet<Type> registeredTypes)
    {
        this.provider = provider;
        this.registeredTypes = registeredTypes;
    }

    public static AutomationCompositionRoot Create(string basePath)
    {
        var configuration = MatcherConfiguration.Load(basePath);
        ValidateGlobal(configuration);
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton(configuration.Automation);
        services.AddSingleton(configuration.Database);
        services.AddSingleton(configuration.Pipeline);
        services.AddSingleton(configuration.Loader);
        services.AddSingleton(configuration.Evidence);
        services.AddSingleton(configuration.Resilience);
        services.AddSingleton<IntegrationConfigurationValidator>();

        services.AddSingleton<IScenarioIdNormalizer, ScenarioIdNormalizer>();
        services.AddSingleton<IDuplicateScenarioDetector, DuplicateScenarioDetector>();
        services.AddSingleton<IRequestXmlMetadataReader, RequestXmlMetadataReader>();
        services.AddSingleton<ITagNormalizer, TagNormalizer>();
        services.AddSingleton<IXmlFingerprintService, XmlFingerprintService>();
        services.AddSingleton<CsvScenarioReader>();
        services.AddSingleton<SchemeRouteResolver>();
        services.AddSingleton<ILoaderRouteResolver>(services => services.GetRequiredService<SchemeRouteResolver>());
        services.AddSingleton<IScenarioRouteResolver>(services => services.GetRequiredService<SchemeRouteResolver>());

        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<ISqlMutationSessionFactory, SqlConnectionMutationSessionFactory>();
        services.AddSingleton<ISqlMutationExecutor, SqlMutationExecutor>();
        services.AddSingleton<IDatabaseRetryExecutor, DatabaseRetryExecutor>();
        services.AddSingleton<IFuzzyMatcherRepository, FuzzyMatcherRepository>();
        services.AddSingleton<ILoaderRepository, ProductionLoaderRepository>();

        services.AddSingleton<IExternalServiceClientFactory, ExternalServiceClientFactory>();
        services.AddSingleton<IExternalAPIRequestUrlBuilder, ExternalAPIRequestUrlBuilder>();
        services.AddSingleton<IExternalAPIRateLimiter>(_ => new ExternalAPIRateLimiter(configuration.Resilience.ApiRateLimitPerSecond));
        services.AddSingleton<IExternalAPIRetryPolicy, ExternalAPIRetryPolicy>();
        services.AddSingleton<IExternalXmlServiceClient, ExternalXmlServiceClient>();
        services.AddSingleton<ILoaderApiClient, ProductionLoaderApiClient>();

        var schemaNames = configuration.Routes.Values.Select(route => route.ResponseSchemaFile).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        services.AddSingleton<ISchemaRegistry>(_ => new SchemaRegistry(Path.Combine(AppContext.BaseDirectory, "Schemas"), schemaNames));
        services.AddSingleton<IXmlSchemaValidator, XmlSchemaValidator>();
        services.AddSingleton<IComparisonAmountReaderRegistry>(_ => new ComparisonAmountReaderRegistry(new[] { new ComparisonAmountReaderRegistration("PlaceholderResponseProcessor", new PlaceholderResponseAmountReader()) }));
        services.AddSingleton<ExternalResponseValidationService>();
        services.AddSingleton<ILoaderResponseValidator, SchemaLoaderResponseValidator>();
        services.AddSingleton<IThresholdEvaluator, ThresholdEvaluator>();

        services.AddSingleton<IEvidencePathBuilder>(_ => new EvidencePathBuilder(configuration.Evidence.EvidenceDirectory));
        services.AddSingleton<IScenarioEvidenceWriter>(_ => new ScenarioEvidenceWriter(_.GetRequiredService<IEvidencePathBuilder>(), configuration.Pipeline.BuildId));
        services.AddSingleton<ILoaderEvidenceWriter>(_ => new LoaderEvidenceWriter(_.GetRequiredService<IEvidencePathBuilder>(), configuration.Pipeline.BuildId));
        services.AddSingleton<ILoaderSummaryWriter, LoaderSummaryWriter>();
        services.AddTransient<LoaderSynchronizationService>();
        services.AddTransient<IComparisonScenarioExecutor, ComparisonScenarioExecutor>();
        services.AddSingleton<IScenarioLogger, NLogScenarioLogger>();
        services.AddSingleton<NUnitScenarioOutputWriter>();

        return new AutomationCompositionRoot(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }), services.Select(service => service.ServiceType).ToHashSet());
    }

    public T GetRequiredService<T>() where T : notnull => provider.GetRequiredService<T>();

    public bool HasRegistration<T>() => registeredTypes.Contains(typeof(T));

    public IServiceScope CreateScope() => provider.CreateScope();

    public void Dispose() => provider.Dispose();

    private static void ValidateGlobal(MatcherConfiguration configuration)
    {
        if (configuration.Automation.MinimumThreshold > configuration.Automation.MaximumThreshold)
            throw new ConfigurationValidationException("Minimum threshold must be less than or equal to maximum threshold.");
        if (configuration.Resilience.ApiRateLimitPerSecond <= 0 || configuration.Resilience.ApiRetryAttempts < 0 || configuration.Resilience.SqlRetryAttempts < 0)
            throw new ConfigurationValidationException("Resilience settings must contain a positive API rate and non-negative retry counts.");
    }
}