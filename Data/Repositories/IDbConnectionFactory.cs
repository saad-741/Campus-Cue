using System.Data.Common;

namespace CampusCue.Data.Repositories;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
