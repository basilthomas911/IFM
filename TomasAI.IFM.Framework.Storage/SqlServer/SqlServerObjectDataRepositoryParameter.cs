using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage.SqlServer
{
    public  class SqlServerObjectDataRepositoryParameter : IObjectRepositoryParameter
    {
        public DbParameter Parameter => new SqlParameter();
    }
}
