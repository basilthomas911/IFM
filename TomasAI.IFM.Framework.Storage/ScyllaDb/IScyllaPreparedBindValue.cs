using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Resolves a prepared marker value after checking the statement's actual CQL type metadata.
/// </summary>
public interface IScyllaPreparedBindValue
{
    /// <summary>Returns the value to bind to the prepared statement.</summary>
    object Resolve(ISession session, PreparedStatement statement);
}
