using ClientAutomationFramework.Core.ExternalAPIAccess;
using ClientAutomationFramework.Core.Matching;
using Microsoft.Extensions.Configuration;

namespace ClientAutomationFramework.Core.Configuration;

public sealed class AppConfiguration
{
    public required DatabaseSettings Database { get; init; }
    public required SchemeConfig XmlScheme { get; init; }
    public required SchemeConfig JsonScheme { get; init; }
    public required MatchSettings Match { get; init; }

    public static AppConfiguration Load(string basePath)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return new AppConfiguration
        {
            Database = configuration.GetSection("Database").Get<DatabaseSettings>() ?? new DatabaseSettings(),
            XmlScheme = configuration.GetSection("XmlScheme").Get<SchemeConfig>() ?? new SchemeConfig(),
            JsonScheme = configuration.GetSection("JsonScheme").Get<SchemeConfig>() ?? new SchemeConfig(),
            Match = configuration.GetSection("Match").Get<MatchSettings>() ?? new MatchSettings()
        };
    }
}
