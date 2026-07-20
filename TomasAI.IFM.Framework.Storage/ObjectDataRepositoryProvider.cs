using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Framework.Storage.SqlServer;

namespace TomasAI.IFM.Framework.Storage
{
    public class ObjectDataRepositoryProvider
    {
        /// <summary>
        /// Create a new instance of the ObjectDataRepositoryProvider   
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="ctx"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static IObjectRepositoryProvider? Create(string providerName, IObjectRepositoryContext ctx, ILogger<DbProvider> logger)
            => providerName switch
            {
                "System.Data.ScyllaDb" => ScyllaDbObjectDataRepositoryProvider.CreateProvider(ctx, logger),
                "System.Data.Postgres" => new PostgresObjectDataRepositoryProvider(ctx, logger),
                _ => new SqlServerObjectDataRepositoryProvider(ctx, logger)
            };
    }
}
