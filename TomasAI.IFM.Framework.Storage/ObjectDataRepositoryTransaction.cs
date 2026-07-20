using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using QLNet;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.SqlServer;

namespace TomasAI.IFM.Framework.Storage
{
    /// <summary>
    /// object repository transaction
    /// </summary>
    /// <typeparam name="TRepo"></typeparam>
    public class ObjectDataRepositoryTransaction
    {
        public static IObjectRepositoryTransaction<TRepo>? Create<TRepo>(string providerName) where TRepo : IObjectRepository
        {
            return providerName switch
            {
                "System.Data.Cassandra" => default,
                "System.Data.Scylla" => default,
                "System.Data.Postgres" => new PostgresObjectDataRepositoryTransaction<TRepo>(),
                _ => new SqlServerObjectDataRepositoryTransaction<TRepo>()
            };
        }
    }
}

