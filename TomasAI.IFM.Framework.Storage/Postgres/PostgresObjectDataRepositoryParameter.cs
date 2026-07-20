using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage.Postgres
{
    public class PostgresObjectDataRepositoryParameter : IObjectRepositoryParameter
    {
        public DbParameter Parameter  => new Npgsql.NpgsqlParameter();    
    }
}
