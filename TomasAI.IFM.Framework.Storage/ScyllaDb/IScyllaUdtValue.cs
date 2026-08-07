using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>Resolves a lazily mapped UDT value against the active Scylla session.</summary>
public interface IScyllaUdtValue
{
    object Resolve(ISession session);
}
