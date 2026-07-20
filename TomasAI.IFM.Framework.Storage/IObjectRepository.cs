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
    Dictionary<Type, object> ResultTypeMap { get; }

    IObjectRepositoryConnection CreateConnection();
    DbParameter CreateParameter();
    void AddResultTypeMap<TEntity>(DbMap<TEntity> mappedEntity);
    IObjectRepositoryContext Use(string commandText);
    //IObjectRepositoryContext Use<TStoredProc>(Expression<Func<TStoredProc, object>> spPropertyNameExpr, bool useParamNameAsDbName = true) where TStoredProc : class;
    //IObjectRepositoryContext Use<TStoredProc>(Expression<Func<TStoredProc, Enum>> spPropertyNameExpr) where TStoredProc : Enum;
    IObjectRepositoryContext Use<TStoredProc>(TStoredProc spPropertyNameEnum) where TStoredProc : Enum;
    IObjectDataReaderContext Use(Func<string, IDataReaderOptions> getReaderOptions);
    IObjectUriContext Use(Uri uriObject);

    //void QueueCommand(string commandText, CommandType commandType, DbParameter[] dbParameters);
    Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false);
    IObjectRepositoryTransaction? BeginTransaction();
    Task LockAsync();
    void Unlock();

    object InTransaction();
}

public interface IObjectRepository<TRepo> : IObjectRepository
{
    IObjectRepository Database { get; }
}
