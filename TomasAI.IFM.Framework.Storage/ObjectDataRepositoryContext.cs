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
        ParameterValues.AddRange(parameterValues.Cast<object>());
        return this;
    }

    /// <summary>
    /// execute query asynchronously and return result set
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataReaderMapper) 
        => _provider.GetObjectsAsync(this, dataReaderMapper);

    /// <summary>
    /// Executes a query asynchronously and maps the results to a collection of objects.
    /// </summary>
    /// <remarks>The <paramref name="dataReaderMapper"/> function is invoked for each record in the query
    /// result set. Ensure that the function is thread-safe if it accesses shared resources.</remarks>
    /// <typeparam name="TResult">The type of the objects in the resulting collection.</typeparam>
    /// <param name="dataReaderMapper">A function that maps a data record to an instance of <typeparamref name="TResult"/>. The function takes a string
    /// and an integer as input parameters and returns a <typeparamref name="TResult"/> object.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of  <typeparamref
    /// name="TResult"/> objects created by the <paramref name="dataReaderMapper"/>.</returns>
    public Task<ICollection<TResult>> ExecuteQueryAsync<TResult>(Func<string, int, TResult> dataReaderMapper)
        => _provider.GetObjectsAsync(this, dataReaderMapper);

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

    /// <summary>
    /// Executes a map-reduce operation asynchronously using the specified mapper and reducer functions.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the mapper function.</typeparam>
    /// <param name="mapper">A function that maps each item to a result of type <typeparamref name="TResult"/>.  Cannot be <see
    /// langword="null"/>.</param>
    /// <param name="reducer">An action that processes the collection of mapped results.  Cannot be <see langword="null"/>.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="mapper"/> or <paramref name="reducer"/> is <see langword="null"/>.</exception>
    public ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(reducer);
        return _provider.ExecuteMapReduceAsync(this, mapper, reducer);
    }

    public ValueTask ExecuteMapReduceAsync<TResult>(Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(reducer);
        return _provider.ExecuteMapReduceAsync(this, mapper, reducer);
    }

    /// execute stored procedure asynchronously and return single result
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectMapReader<TResult>, TResult> dataReaderMapper) 
        => _provider.GetObjectAsync(this, dataReaderMapper);

    /// <summary>
    /// Executes the query and maps the first row to a single object using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public Task<TResult?> ExecuteSingleAsync<TResult>(Func<IObjectDataRecord, TResult> dataReaderMapper)
        => _provider.GetObjectAsync(this, dataReaderMapper);

    public Task<TResult?> ExecuteSingleAsync<TResult>(IObjectDataRecord dataReaderMapper)
    { 
        //_provider.GetObjectAsync(this, dataReaderMapper);
        return Task.FromResult<TResult?>(default);
    }

    /// <summary>
    ///  return scalar result
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dataReaderMapper"></param>
    /// <returns></returns>
    public Task<TResult> ExecuteScalarAsync<TResult>(Func<IObjectMapReader<Scalar<TResult>>, TResult> dataReaderMapper) where TResult : struct
        => _provider.GetObjectFromSourceAsync(this, dataReaderMapper);

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
