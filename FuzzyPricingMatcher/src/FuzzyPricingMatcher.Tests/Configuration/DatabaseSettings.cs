namespace FuzzyPricingMatcher.Tests.Configuration;

public sealed class DatabaseSettings
{
    public string SqlConnectionString { get; set; } = string.Empty;
    public string RequestTableName { get; set; } = "dbo.tb_xmlrequest";
    public string ResponseTableName { get; set; } = "dbo.tb_response";
}