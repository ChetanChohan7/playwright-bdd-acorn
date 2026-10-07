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

	public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
	{
		var connection = Create();
		try
		{
			await connection.OpenAsync(cancellationToken);
			return connection;
		}
		catch
		{
			await connection.DisposeAsync();
			throw;
		}
	}
}
