using System.Data;

namespace Occ.SharedKernal;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}