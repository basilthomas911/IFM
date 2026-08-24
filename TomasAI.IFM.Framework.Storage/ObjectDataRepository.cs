using System.Data;
using System.Data.Common;
using TomasAI.IFM.Shared.Storage;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage;

public abstract class ObjectDataRepository<TRepo> : IObjectRepository<TRepo> where TRepo : IObjectRepository
{
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
    public CommandType QueuedCommandType => default;

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
    /// use command text context
    /// </summary>
    /// <param name="commandName">Globally identifiable command name.</param>
    /// <param name="commandText">Command text.</param>
    /// <returns></returns>
    public IObjectRepositoryContext Use(string commandName, string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("ObjectDataRepository.UseCommandText: commandName parameter is empty");
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("ObjectDataRepository.UseCommandText: commandText parameter is empty");
        _logger.LogDebug(
            "ObjectDataRepository.UseCommandText: command name: {CommandName}{NewLine}{CommandText}",
            commandName,
            Environment.NewLine,
            commandText);
        return _provider.CreateCommandTextContext(commandName, commandText);
    }

    public IObjectUriContext Use(string commandName, Uri uriObject)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("ObjectDataRepository.Use: commandName parameter is empty");
        if (uriObject is null)
            throw new ArgumentException("ObjectDataRepository.Use: Uri parameter is empty");

        _logger.LogDebug(
            "ObjectDataRepository.UseUri: command name: {CommandName}{NewLine}{Uri}",
            commandName,
            Environment.NewLine,
            uriObject);

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
            return _provider.CreateFileUriContext(commandName, uriObject, dataReaderOptions!);
        }
    }
    /// <summary>
    /// execute all queued commands
    /// </summary>
    /// <returns></returns>
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
    {
        ArgumentNullException.ThrowIfNull(queuedCommands);
        if (queuedCommands.Count == 0)
        {
            throw new StorageException(
                "ObjectDataRepository.ExecuteQueuedCommandsAsync: no commands have been queued");
        }

        await _provider
            .CreateQueuedCommandsContext(queuedCommands)
            .ExecuteQueuedCommandsAsync(queuedCommands, useTransaction);
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
}
