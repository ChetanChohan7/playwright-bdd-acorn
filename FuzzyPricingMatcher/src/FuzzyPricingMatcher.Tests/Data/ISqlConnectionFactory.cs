using Microsoft.Data.SqlClient;

namespace FuzzyPricingMatcher.Tests.Data;

public interface ISqlConnectionFactory
{
    SqlConnection Create();
}