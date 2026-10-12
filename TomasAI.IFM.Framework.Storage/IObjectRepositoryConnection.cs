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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserves the existing externally referenced identity and named API contract.")]
        TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "Generic marker binds repository/connection identity for dependency injection.")]
    public interface IObjectRepositoryConnection<TConnection> : IObjectRepositoryConnection
    {

    }
}
