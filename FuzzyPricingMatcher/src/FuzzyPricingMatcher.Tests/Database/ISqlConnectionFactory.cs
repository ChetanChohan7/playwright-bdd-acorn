using Microsoft.Data.SqlClient;

namespace FuzzyPricingMatcher.Tests.Database;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}