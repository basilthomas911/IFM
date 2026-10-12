using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;

using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Framework.Storage.SqlServer
{
    public class SqlServerObjectDataRepositoryConnection : IObjectRepositoryConnection<SqlConnection>
    {
        public TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection
        {
            if (typeof(TConnection) != typeof(SqlConnection))
                throw new NotSupportedException("SQL Server connections require SqlConnection.");
            return (TConnection)(IDbConnection)new SqlConnection(connectionString);
        }
    }
}
