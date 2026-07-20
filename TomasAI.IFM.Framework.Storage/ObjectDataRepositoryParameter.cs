using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.SqlServer;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataRepositoryParameter
    {
        public static IObjectRepositoryParameter? Create(string providerName)
           => providerName switch
           {
               "System.Data.Cassandra" => null,
               "System.Data.Scylla" => null,
               "System.Data.Postgres" => new PostgresObjectDataRepositoryParameter(),
               null or _ => new SqlServerObjectDataRepositoryParameter()
           };
    }
}
