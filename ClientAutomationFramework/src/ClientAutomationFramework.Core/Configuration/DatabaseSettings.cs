namespace ClientAutomationFramework.Core.Configuration;

public sealed class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string RequestTableName { get; set; } = "XML_Requests";
    public string ResponseTableName { get; set; } = "XML_Response";
}
