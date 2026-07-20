using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectRepositoryConnection
    {
        TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection;
    }

    public interface IObjectRepositoryConnection<TConnection> : IObjectRepositoryConnection
    {

    }
}
