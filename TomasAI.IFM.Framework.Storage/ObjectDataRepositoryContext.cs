using System.Data;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage;

public abstract class ObjectDataRepositoryContext : IObjectRepositoryContext, IDisposable
{
    readonly IObjectRepository _db;
    List<object>? _parameterValues;
      IObjectRepositoryProvider _provider;   
    bool _useTransaction;
    int _commandTimeout;
    ILogger<DbProvider> _logger;

    /// <summary>
    /// create repository context
    /// </summary>
    /// <param name="db"></param>
    public ObjectDataRepositoryContext(IObjectRepository db, ILogger<DbProvider> logger)
    {
        if (db == null)
            throw new ArgumentException("ObjectDataRepositoryContext: base repository parameter is empty");
        _db = db;
        _useTransaction = true;
        _commandTimeout = -1;
        _logger = logger;
        _provider = ObjectDataRepositoryProvider.Create(db.ProviderName, this,  logger)!;
        if (_provider == null)
            throw new ArgumentException($"ObjectDataRepositoryContext: unable to create Db Provider: {db.ProviderName}");

    }

    /// <summary>
    /// override in derived class to set command type to stored procedure or command text
    /// </summary>
    /// <param name="cmd"></param>
    public abstract void SetCommand(IDbCommand cmd);
    public abstract CommandType GetCommandType();
    public abstract string GetCommandText();
    public abstract string GetParameterName(string parameterName);
    public List<object> ParameterValues => _parameterValues ??= [];
    public bool UseTransaction => _useTransaction;
    public int CommandTimeout => _commandTimeout;
    public IObjectRepository Repository => _db;

    public string CommandText => GetCommandText();

    /// <summary>
    /// set stored procedure parameters
    /// </summary>
    /// <returns></returns>
    public IObjectRepositoryContext SetParameters(object parameterValue = default!)
    {
        ParameterValues.Clear();
        if (parameterValue is  null)
            throw new ArgumentException("ObjectDataRepositoryContext.SetParameters: must set parameter value to parameter type ");
        ParameterValues.Add(parameterValue);
        return this;
    }

    public IObjectRepositoryContext SetParameters<TParam>(in TParam parameterValue) where TParam : struct, IBindValue
    {
        ParameterValues.Clear();
        ParameterValues.Add(parameterValue.Bind());
        return this;
    }

    public IObjectRepositoryContext SetParameters<TParam>(IEnumerable<TParam> parameterValues)
    {
        ParameterValues.Clear();
        if (parameterValues is null) throw new ArgumentException("ObjectDataRepositoryContext.SetParameters<TParam>: must set parameter values to parameter type ");
        foreach (var parameterValue in parameterValues)
        {
            ParameterValues.Add(parameterValue is IBindValue bindValue
                ? bindValue.Bind()
                : parameterValue!);
        }
        return this;
    }

    /// <summary>
    /// Streams query results asynchronously using an ordinal record mapper.
    /// The provider owns its database resources until enumeration completes or the enumerator is disposed.
    /// </summary>
    public IAsyncEnumerable<TResult> ExecuteStreamAsync<TResult>(
        Func<IObjectDataRecord, TResult> dataMapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataMapper);
        return _provider.StreamObjectsAsync(this, dataMapper, cancellationToken);
    }

    /// <summary>
    /// Executes the query asynchronously and maps the results to a collection using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper)
        => _provider.GetObjectsAsync(this, dataReaderMapper);

    /// <summary>
    /// Executes the query asynchronously and maps the results to a pooled, read-only buffer using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public Task<IReadOnlyList<TResult>> ExecuteQueryImmutableAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper) where TResult : struct
        => _provider.GetImmutableObjectsAsync(this, dataReaderMapper);

    public ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(reducer);
        return _provider.ExecuteMapReduceAsync(this, mapper, reducer);
    }

    /// <summary>
    /// Executes the query and maps the first row to a single object using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper)
        => _provider.GetObjectAsync(this, dataReaderMapper);

    /// <summary>
    /// Executes a scalar query asynchronously and maps the result using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper) where TResult : struct
      => _provider.GetScalarAsync(this, dataReaderMapper);

    /// <summary>
    /// execute command stored procedure asynchronpusly
    /// </summary>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public Task<long[]> ExecuteCommandAsync(Action<string> onInfoMessage = null!) 
        => _provider.ExecuteCommandAsync(this, onInfoMessage);

    /// <summary>
    /// return execution command parameters
    /// </summary>
    /// <returns></returns>
    //public void QueueCommand() => _db.QueueCommand(GetCommandText(), GetCommandType(), GetQueuedCommandParameters().SingleOrDefault());
    public object QueueCommand()
        => _provider.QueueCommand(GetCommandText(), GetCommandType(), ParameterValues);

    /// <summary>
    /// execute list of command stored procedure 
    /// </summary>
    /// <returns></returns>
    public Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false) 
        => _provider.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction);

    public void Dispose()
    {
        if (_parameterValues is not null)
        {
            _parameterValues.Clear();
            _parameterValues = null;
        }

    }

    
}
