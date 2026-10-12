namespace PricingValidationFramework.Core.Configuration;

public class DatabaseSettings
{
	public string ConnectionString { get; set; } = string.Empty;

	/// Written to row_createdby / row_modifiedby on the rows this code inserts or updates.
	/// Empty means SQL Server's SUSER_SNAME(): the login running the statement.
	public string AuditUser { get; set; } = string.Empty;
}
