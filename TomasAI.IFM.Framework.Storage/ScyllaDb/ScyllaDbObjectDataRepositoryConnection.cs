using System.Data;
using Cassandra;
using Cassandra.Serialization;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public class ScyllaDbObjectDataRepositoryConnection : IObjectRepositoryConnection<ScyllaDbConnection>
{
    /// <summary>
    /// Create a ScyllaDb connection
    /// </summary>
    /// <typeparam name="TConnection"></typeparam>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public TConnection As<TConnection>(string connectionString) where TConnection : class, IDbConnection
        => throw new NotSupportedException($"{nameof(ScyllaDbObjectDataRepositoryConnection)}.As<TConnection>: create ScyllaDbConnection directly in provider code");
}
