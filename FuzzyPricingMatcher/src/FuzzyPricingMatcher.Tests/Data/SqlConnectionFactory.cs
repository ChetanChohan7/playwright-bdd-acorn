using FuzzyPricingMatcher.Tests.Configuration;
using Microsoft.Data.SqlClient;

namespace FuzzyPricingMatcher.Tests.Data;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string connectionString;

    public SqlConnectionFactory(DatabaseSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SqlConnectionString))
            throw new DatabaseOperationException("SQL connection string is required when database access is enabled.");
        connectionString = settings.SqlConnectionString;
    }

    public SqlConnection Create() => new(connectionString);
}