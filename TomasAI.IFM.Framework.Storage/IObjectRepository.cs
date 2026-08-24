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

    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    IObjectRepositoryTransaction? BeginTransaction();

    object InTransaction();
}

public interface IObjectRepository<TRepo> : IObjectRepository
{
    IObjectRepository Database { get; }
}
