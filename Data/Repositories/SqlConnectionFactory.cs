using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace CampusCue.Data.Repositories;

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public DbConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The 'DefaultConnection' connection string is not configured.");
        }

        return new SqlConnection(connectionString);
    }
}
