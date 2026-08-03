using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Npgsql;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Framework.Storage.Postgres
{
    public class PostgresObjectDataRepositoryConnection : IObjectRepositoryConnection<NpgsqlConnection>
    {
        public TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection
        {
           var credentials = DatabaseCredentialResolver.Resolve(DatabaseProvider.Postgres);
           var resolvedConnectionString = DatabaseCredentialResolver.AddPostgresCredentials(connectionString, credentials);
           return new NpgsqlConnection(resolvedConnectionString) as TConnection;
        }
    }
}
