namespace PricingValidationFramework.Core.Database;

using Microsoft.Data.SqlClient;
using PricingValidationFramework.Core.Configuration;

public class SqlConnectionFactory
{
	private readonly DatabaseSettings settings;

	public SqlConnectionFactory(DatabaseSettings settings)
	{
		this.settings = settings;
	}

	public SqlConnection Create()
	{
		return new SqlConnection(settings.ConnectionString);
	}
}
