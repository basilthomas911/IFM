using System.Data;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectRepository
{
    string ConnectionString { get; }
    string ProviderName { get; }
    string StoredProcedureName { get; set; }
    string CommandText { get; set; }
    string Schema { get; set; }
    CommandType CommandType { get; }
    CommandType QueuedCommandType { get; }
    IObjectRepositoryConnection CreateConnection();
    DbParameter CreateParameter();
    IObjectRepositoryContext Use(string commandName, string commandText);
    IObjectUriContext Use(string commandName, Uri uriObject);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    IObjectRepositoryTransaction? BeginTransaction();

    object InTransaction();
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "Generic marker binds repository/connection identity for dependency injection.")]
public interface IObjectRepository<TRepo> : IObjectRepository
{
    IObjectRepository Database { get; }
}
