using TomasAI.IFM.Framework.Storage.SqlServer;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataRepositoryConnection
    {
        public static IObjectRepositoryConnection Create(string providerName)
            => providerName switch
            {
                "System.Data.ScyllaDb" => new ScyllaDbObjectDataRepositoryConnection(),
                "System.Data.Postgres" => new PostgresObjectDataRepositoryConnection(),
                null or _ => new SqlServerObjectDataRepositoryConnection()
            };
    }
}
