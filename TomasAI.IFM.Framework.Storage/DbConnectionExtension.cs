using System;
using System.Data;

namespace TomasAI.IFM.Framework.Storage
{
    public static class DbConnectionExtension
    {

        extension(IDbConnection dbConn)
        {
            public IDbConnection SetConnectionString(string connectionString)
            {
                if (dbConn == null)
                    throw new InvalidOperationException("DbConnectionExtension.SetConnectionString: dbConnection parameter is null");
                dbConn.ConnectionString = connectionString;
                return dbConn;
            }
        }
    }

}
