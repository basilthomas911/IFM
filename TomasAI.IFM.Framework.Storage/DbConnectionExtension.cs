using System;
using System.Data;

namespace TomasAI.IFM.Framework.Storage
{
    public static class DbConnectionExtension
    {
        public static IDbConnection SetConnectionString(this IDbConnection dbConn, string connectionString)
        {
            if (dbConn == null)
                throw new InvalidOperationException("DbConnectionExtension.SetConnectionString: dbConnection parameter is null");
            dbConn.ConnectionString = connectionString;
            return dbConn;
        }
    }

}
