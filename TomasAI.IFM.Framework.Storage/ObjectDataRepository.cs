using System.Linq.Expressions;
using System.Data;
using System.Data.Common;
using TomasAI.IFM.Shared.Storage;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage;

public abstract class ObjectDataRepository<TRepo> : IObjectRepository<TRepo> where TRepo : IObjectRepository
{
    readonly static SemaphoreSlim _semaphoreSlim = new(1, 1);
    readonly Dictionary<Type, object> _resultTypeMap;
    readonly IObjectCreateProvider _provider;
    IObjectRepositoryTransaction<TRepo>? _transaction;
    readonly ILogger _logger;

    /// <summary>
    /// create db context from base class
    /// </summary>
    /// <param name="connectionSetting"></param>
    public ObjectDataRepository(IDbConnectionSetting connectionSetting, ILogger<DbProvider> logger)
    {
        // only initialize object data repos that have been configured from startup settings...
        _resultTypeMap = [];
        _transaction = null;
        _logger = logger;
        _provider = new ObjectDataDbProvider(this, logger);
        if (connectionSetting == null) 
            return;
        ConnectionSetting = connectionSetting;
        ConnectionString = connectionSetting.ConnectionString;
        if (string.IsNullOrEmpty(connectionSetting.ProviderName))
            throw new InvalidOperationException($"ObjectDataRepository: no provider name set for connection string '{ConnectionString}'");
        ProviderName = connectionSetting.ProviderName;
        OnCreateModel(new DbModel<TRepo>(this));
    }

    /// <summary>
    /// public properties
    /// </summary>
    protected IDbConnectionSetting ConnectionSetting { get; } = null!;
    public abstract IObjectRepository Database { get; }
    public string ConnectionString { get; } = string.Empty;
    public string ProviderName { get; } = string.Empty;
    public string StoredProcedureName { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public CommandType CommandType { get; private set; } = CommandType.Text;
    public Dictionary<Type, object> ResultTypeMap => _resultTypeMap;

    public CommandType QueuedCommandType => default;

    public virtual void OnCreateModel(DbModel<TRepo> model)
    {
    }

    /// <summary>
    /// type map expression that will be source of entity property map
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="typeMapPropertyExpr"></param>
    /// <returns></returns>
    public IObjectTypeMapper<TEntity> Map<TEntity>(Expression<Func<TRepo, DbMap<TEntity>>> typeMapPropertyExpr, string tableName = null)
    {
        if (typeMapPropertyExpr is null)
            throw new ArgumentException("ObjectDataRepository.Map: type map property expression is empty");
        if (typeMapPropertyExpr.Body is not MemberExpression propExpr)
            throw new InvalidOperationException($"ObjectDataRepository.Map: type map property expression does not map to property of '{typeof(TEntity).Name}'");
        tableName = string.IsNullOrWhiteSpace(tableName) ? propExpr.Member.Name : tableName;
        return new ObjectDataTypeMapper<TEntity>(this, tableName);
    }

    /// <summary>
    /// add mapped entity to result type map
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dbMap"></param>
    public void AddResultTypeMap<TResult>(DbMap<TResult> dbMap)
    {
        if (dbMap is null)
            throw new ArgumentException("ObjectDataRepository.AddResultTypeMap: dbMap parameter is empty");
        if (_resultTypeMap.ContainsKey(typeof(TResult)))
            _resultTypeMap.Remove(typeof(TResult));
        _resultTypeMap.Add(typeof(TResult), dbMap);
    }

        /// <summary>
    /// create db connection using create provider
    /// </summary>
    /// <returns></returns>
    public IObjectRepositoryConnection CreateConnection()
        => _provider.CreateConnection();

    /// <summary>
    /// create db parameter using create provider
    /// </summary>
    /// <returns></returns>
    public DbParameter CreateParameter()
        => _provider.CreateParameter().Parameter;

    /// <summary>
    /// set stored procedure name
    /// </summary>
    /// <param name="storedProcedure"></param>
    /// <returns></returns>
    public IObjectRepositoryContext Use<TStoredProc>(Expression<Func<TStoredProc,object>> spPropertyNameExpr, bool useParamNameAsDbName = true) where TStoredProc : class
    {
        if (spPropertyNameExpr.Body is not MemberExpression memberExpr)
            throw new ArgumentException($"ObjectDataRepository.UseStoredProcedure: parameter MUST be a property from '{typeof(TStoredProc).Name}'");
        var paramExpr = spPropertyNameExpr.Parameters[0];
        var storedProcName = useParamNameAsDbName
            ? $"{paramExpr.Name}.{memberExpr.Member.Name}"
            : $"{memberExpr.Member.Name}";
        return _provider.CreateStoredProcedureContext(storedProcName);
    }

    /// <summary>
    /// set stored procedure name from enumeration expression
    /// </summary>
    /// <param name="spPropertyNameExpr"></param>
    /// <returns></returns>
    public IObjectRepositoryContext Use<TStoredProc>(Expression<Func<TStoredProc, Enum>> spPropertyNameExpr) where TStoredProc:Enum
    {
        if (spPropertyNameExpr.Body is not UnaryExpression enumExpr)
            throw new ArgumentException($"ObjectDataRepository.UseStoredProcedure: parameter MUST be an enumeration property from '{typeof(TStoredProc).Name}'");
        var paramExpr = spPropertyNameExpr.Parameters[0];
        var storedProcName = $"{paramExpr.Name}.{enumExpr.Operand}";
        return _provider.CreateStoredProcedureContext(storedProcName);
    }

    /// <summary>
    /// set stored procedure name from enumeration
    /// </summary>
    /// <param name="spPropertyNameEnum"></param>
    /// <returns></returns>
    public IObjectRepositoryContext Use<TStoredProc>(TStoredProc spPropertyNameEnum) where TStoredProc : Enum
    {
        var storedProcName = $"{spPropertyNameEnum}";
        return _provider.CreateStoredProcedureContext(storedProcName);
    }

    /// <summary>
    /// use command text context
    /// </summary>
    /// <param name="commandText">command text</param>
    /// <returns></returns>
    public IObjectRepositoryContext Use(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("ObjectDataRepository.UseCommandText: commandText parameter is empty");
        _logger.LogDebug($"ObjectDataRepository.UseCommandText: command text '{commandText}'");
        return _provider.CreateCommandTextContext(commandText);
    }

    /// <summary>
    /// use bulk copy context
    /// </summary>
    /// <param name="commandText">command text</param>
    /// <returns></returns>
    public IObjectBulkCopyContext Use(DataTable bulkCopyDataTable)
    {
        if (bulkCopyDataTable is null)
            throw new ArgumentException("ObjectDataRepository.Use: bulkCopyDataTable parameter is empty");
        return _provider.CreateBulkCopyContext(bulkCopyDataTable);
    }

    /// <summary>
    /// use data reader context
    /// </summary>
    /// <param name="getReaderOptions"></param>
    /// <returns></returns>
    public IObjectDataReaderContext Use(Func<string, IDataReaderOptions> getReaderOptions)
    {
        if (getReaderOptions is null)
            throw new ArgumentException("ObjectDataRepository.Use: getReaderOptions parameter is empty");
        return _provider.CreateDataReaderContext(getReaderOptions(this.ConnectionString));
    }

    public IObjectUriContext Use(Uri uriObject)
    {
        if (uriObject is null)
            throw new ArgumentException("ObjectDataRepository.Use: Uri parameter is empty");

        // get uri type..
        var uriContext = uriObject.Scheme.ToLowerInvariant() switch
        {
            "file" => CreateFileUriContext(),
            //"http" or "https" => _provider.CreateHttpUriContext(uriObject, default!),
            _ => throw new NotSupportedException($"ObjectDataRepository.Use: Uri scheme '{uriObject.Scheme}' is not supported")
        };
        return uriContext;

        IObjectUriContext CreateFileUriContext()
        {
            if (uriObject is null)
                throw new ArgumentException("ObjectDataRepository.Use: Uri parameter is empty");
            var dataReaderOptions = new DataReaderOptions(ConnectionSetting.ConnectionString);
            return _provider.CreateFileUriContext(uriObject, dataReaderOptions!);
        }
    }
    /// <summary>
    /// execute all queued commands
    /// </summary>
    /// <returns></returns>
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
        =>  await _provider
                .CreateQueuedCommandsContext()
                .ExecuteQueuedCommandsAsync(queuedCommands, useTransaction);
    
    public IObjectRepositoryContext Use(Expression<Func<TRepo, IEnumerable<object>>> commandExpr)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// start database transaction that will span over multiple object repo execution/query calls
    /// </summary>
    /// <returns></returns>
    public IObjectRepositoryTransaction? BeginTransaction() 
    {
        _transaction = _provider?.CreateTransaction<TRepo>()?.BeginTransaction(this);
        return _transaction;    
    }
    internal void SetTransactionCompleted() => _transaction = null;
    public object? InTransaction() => _transaction?.CreateCommand();

    public async Task LockAsync()
        => await _semaphoreSlim.WaitAsync();

    public void Unlock()
        => _semaphoreSlim.Release();
}
