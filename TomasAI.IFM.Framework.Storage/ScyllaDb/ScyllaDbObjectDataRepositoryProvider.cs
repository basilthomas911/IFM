using Cassandra;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Provides functionality for interacting with a ScyllaDB database as an object repository provider.
/// </summary>
/// <remarks>This class implements the <see cref="IObjectRepositoryProvider"/> interface and offers methods for
/// executing commands, queuing commands, retrieving objects, and performing bulk operations against a ScyllaDB
/// database. It supports both synchronous and asynchronous operations, and includes mechanisms for handling retries,
/// batching, and transactions.</remarks>
public class ScyllaDbObjectDataRepositoryProvider : IObjectRepositoryProvider
{
    static readonly ConcurrentDictionary<string, IObjectRepositoryProvider> _providers = [];
    const string ClassName = nameof(ScyllaDbObjectDataRepositoryProvider);
    readonly ILogger<DbProvider> _logger;
    readonly ScyllaDbConnection _conn;
    readonly ConcurrentDictionary<string, PreparedStatement> _preparedStatementCache = [];

    /// <summary>
    /// Creates or retrieves an <see cref="IObjectRepositoryProvider"/> instance for the specified context.
    /// </summary>
    /// <remarks>If a provider for the given context's connection string already exists, it is returned. 
    /// Otherwise, a new provider is created, added to the internal cache, and returned.</remarks>
    /// <param name="ctx">The repository context containing the connection string and other configuration details. Cannot be <see
    /// langword="null"/>.</param>
    /// <param name="logger">The logger instance used for logging operations. Cannot be <see langword="null"/>.</param>
    /// <returns>An <see cref="IObjectRepositoryProvider"/> instance associated with the specified context.</returns>
    public static IObjectRepositoryProvider CreateProvider(IObjectRepositoryContext ctx, ILogger<DbProvider> logger)
    {
        var key = ctx.Repository.ConnectionString;
        return _providers.GetOrAdd(key, _ => new ScyllaDbObjectDataRepositoryProvider(ctx, logger));
    }

    /// <summary>
    /// create scylladb object data repository provider 
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="logger">   </param>
    ScyllaDbObjectDataRepositoryProvider(IObjectRepositoryContext ctx, ILogger<DbProvider> logger)
    {
        _logger = logger;
        _conn = new ScyllaDbConnection(ctx.Repository.ConnectionString);
    }

    /// <summary>
    /// Returns a cached <see cref="PreparedStatement"/> for the given CQL text, or prepares and caches one
    /// on first access. Eliminates repeated <c>session.Prepare()</c> round-trips for the same query.
    /// </summary>
    PreparedStatement GetOrPrepare(ISession session, string cql)
        => _preparedStatementCache.GetOrAdd(cql, static (key, s) => s.Prepare(key), session);

    /// <summary>
    /// execute command 
    /// </summary>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public async Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null!)
    {
        for (int retryCount = 1; retryCount <= 5; retryCount++)
        {
            var batchSize = retryCount switch
            {
                1 => 2000,
                2 =>1000,
                3 => 500,
                4 => 250,
                _ => 100
            };
            try
            {
                _logger.LogInformationEvent(ClassName, "ExecuteCommandAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
                var session = await _conn.CreateSessionAsync();
                if (ctx.ParameterValues.Count > 1)
                    await ExecuteBatchCommandsAsync(session, batchSize);
                else if (ctx.ParameterValues.Count == 1)
                    await ExecuteSingleCommandsAsync(session);
                else
                    await session.ExecuteAsync(new SimpleStatement(ctx.CommandText));
                return [-1L];
            }
            catch (Exception ex)
            {
                if (retryCount < 5)
                {
                    await Task.Delay(1000);
                    continue;
                }
                while (ex.InnerException != null) ex = ex.InnerException;
                var errorMessage = $"{ClassName}.ExecuteCommandAsync: {ctx.CommandText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }
        return [-1L];

        async Task ExecuteBatchCommandsAsync(ISession session, int batchSize)
        {
            var ps = GetOrPrepare(session, ctx.CommandText);
            for (var i = 0; i < ctx.ParameterValues.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, ctx.ParameterValues.Count - i);
                var items = ctx.ParameterValues.GetRange(i, count);
                var batchStatement = new BatchStatement();
                batchStatement.SetBatchType(BatchType.Logged);
                batchStatement.SetSerialConsistencyLevel(ConsistencyLevel.Serial);
                foreach (var bindValues in items)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    batchStatement.Add(boundStatement);
                }
                await session.ExecuteAsync(batchStatement);
                batchStatement = null;
            }
        }

        async Task ExecuteSingleCommandsAsync(ISession session)
        {
            var ps = GetOrPrepare(session, ctx.CommandText);
            var bindValues = ctx.ParameterValues[0];
            var boundStatement = bindValues is not null
                ? ps.Bind(bindValues)
                : ps.Bind();
            await session.ExecuteAsync(boundStatement);
        }
    }

    /// <summary>
    /// queue command for execution
    /// </summary>
    /// <param name="commandText"></param>
    /// <param name="commandType"></param>
    /// <param name="bindValues"></param>
    /// <exception cref="ArgumentException"></exception>
    public object QueueCommand(string commandText, CommandType commandType, List<object> bindValues)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new StorageException($"{ClassName}.QueueCommand: command text parameter is empty");
        return new ScyllaDbObjectDataQueuedCommand(commandType, commandText, bindValues);
    }

    /// <summary>
    /// execute list of queued commands 
    /// </summary>
    /// <returns></returns>
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
    {
        if (queuedCommands.Count == 0)
            throw new StorageException($"{ClassName}.ExecuteQueuedCommandsAsync: no commands have been queued for execution");
        var commandText = string.Empty;
        try
        {
            var session = await _conn.CreateSessionAsync();
            if (!useTransaction)
                await ExecuteQueuedCommandsSequentiallyAsync(session);
            else
                await ExecuteQueuedCommandsAsBatchAsync(session);
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.ExecuteQueuedCommandAsync: {commandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }

        async Task ExecuteQueuedCommandsSequentiallyAsync(ISession session)
        {
            foreach (ScyllaDbObjectDataQueuedCommand cmd in queuedCommands.Cast<ScyllaDbObjectDataQueuedCommand>())
            {
                if (cmd is null) continue;
                _logger.LogInformationEvent(ClassName, "ExecuteQueuedCommandsAsync: {CommandText} with {BindValuesCount} bind values", cmd.CommandText, cmd.BindValues?.Count ?? 0);
                commandText = cmd.CommandText;
                if (cmd.BindValues!.Count > 0)
                {
                    var ps = GetOrPrepare(session, commandText);
                    foreach (var bindValues in cmd.BindValues)
                    {
                        var boundStatement = bindValues is not null
                            ? ps.Bind(bindValues)
                            : ps.Bind();
                        await session.ExecuteAsync(boundStatement);
                    }
                }
                else
                {
                    var simpleStatement = new SimpleStatement(commandText);
                    await session.ExecuteAsync(simpleStatement);
                }
            }
        }

        async Task ExecuteQueuedCommandsAsBatchAsync(ISession session)
        {
            var batchStatement = new BatchStatement();
            batchStatement.SetBatchType(BatchType.Logged);
            batchStatement.SetSerialConsistencyLevel(ConsistencyLevel.Serial);
            foreach (ScyllaDbObjectDataQueuedCommand cmd in queuedCommands.Cast<ScyllaDbObjectDataQueuedCommand>())
            {
                _logger.LogInformationEvent(ClassName, "ExecuteQueuedCommandsAsync: {CommandText} with {BindValuesCount} bind values", cmd.CommandText, cmd.BindValues?.Count ?? 0);
                var ps = GetOrPrepare(session, cmd.CommandText);
                var bindValues = cmd.BindValues!.Count == 1 ? cmd.BindValues[0]: default;
                var boundStatement = bindValues is not null
                            ? ps.Bind(cmd.BindValues[0])
                            : ps.Bind();
                batchStatement.Add(boundStatement);
            }
            await session.ExecuteAsync(batchStatement);
            batchStatement = null;
        }
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        if (dataReaderMapper is null)
            throw new StorageException($"{ClassName}.GetObjectsAsync: dataReaderMapper parameter is null"); 
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectsAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectsAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            List<TResult> resultSet = [];
            var session = await _conn.CreateSessionAsync();
            ScyllaDbObjectDataMapReader<TResult>? mapReader = null;
            if (ctx.ParameterValues.Count > 0)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                foreach (var bindValues in ctx.ParameterValues)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    using var rowSet = await session.ExecuteAsync(boundStatement);
                    foreach (var row in rowSet)
                    {
                        mapReader = mapReader is null ? new ScyllaDbObjectDataMapReader<TResult>(row) : mapReader.SetRow(row);
                        resultSet.Add(dataReaderMapper(mapReader));
                    }
                }
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                using var rowSet = await session.ExecuteAsync(simpleStatement);
                foreach (var row in rowSet)
                {
                    mapReader = mapReader is null ? new ScyllaDbObjectDataMapReader<TResult>(row) : mapReader.SetRow(row);
                    resultSet.Add(dataReaderMapper(mapReader));
                }
            }
            return resultSet;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectsAsync: { ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a database query asynchronously and maps the results to a collection of objects of type <typeparamref
    /// name="TResult"/>.
    /// </summary>
    /// <remarks>This method supports both parameterized and non-parameterized queries. If parameter values
    /// are provided in  <paramref name="ctx"/>, the query will be executed for each set of parameter values. Otherwise,
    /// the query will  be executed as a simple statement. The method logs the query execution details and handles
    /// exceptions by wrapping  them in a <see cref="StorageException"/>.</remarks>
    /// <typeparam name="TResult">The type of objects to map the query results to.</typeparam>
    /// <param name="ctx">The context containing the query command text and parameter values.  The <see
    /// cref="IObjectRepositoryContext.CommandText"/> property specifies the query to execute,  and <see
    /// cref="IObjectRepositoryContext.ParameterValues"/> provides the parameter values for the query.</param>
    /// <param name="dataMapper">A function that maps a row's column values to an object of type <typeparamref name="TResult"/>.  The function
    /// takes a column name and its index as input and returns the mapped object.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of objects of type
    /// <typeparamref name="TResult"/>  representing the query results. If no results are found, the collection will be
    /// empty.</returns>
    /// <exception cref="StorageException">Thrown if <paramref name="dataMapper"/> is <see langword="null"/>, if multiple parameter values are provided in 
    /// <paramref name="ctx"/>, or if an error occurs during query execution.</exception>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<string, int, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.GetObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectsAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectsAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            List<TResult> resultSet = [];
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count > 0)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                foreach (var bindValues in ctx.ParameterValues)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    using var rowSet = await session.ExecuteAsync(boundStatement);
                    resultSet = GetResultSet(rowSet, dataMapper);
                }
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                using var rowSet = await session.ExecuteAsync(simpleStatement);
                resultSet = GetResultSet(rowSet, dataMapper);
            }
            return resultSet;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectsAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dataReaderMapper"></param>
    /// <returns></returns>
    /// <exception cref="StorageException"></exception>
    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        if (dataReaderMapper is null)
            throw new StorageException($"{ClassName}.GetObjectAsync: dataReaderMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectAsync: only single parameter value accepted");
        var result = default(TResult);
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var rowSet = default(RowSet);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count == 1)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                var boundStatement = ctx.ParameterValues[0] is not null
                    ? ps.Bind(ctx.ParameterValues[0])
                    : ps.Bind();
                rowSet = await session.ExecuteAsync(boundStatement);
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                rowSet = await session.ExecuteAsync(simpleStatement);
            }
            var row = rowSet.SingleOrDefault();
            if (row is not null)
            {
                var mapReader = new ScyllaDbObjectDataMapReader<TResult>(row);
                result = dataReaderMapper(mapReader);
            }
            return result!;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }


    /// <summary>
    /// execute query that returns a single object
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="dataReaderMapper"></param>
    /// <returns></returns>
    /// <exception cref="StorageException"></exception>
    public async Task<TResult> GetObjectFromSourceAsync<TSource, TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TSource>, TResult> dataReaderMapper)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectAsync<{typeof(TSource).Name}, {typeof(TResult).Name}>: only single parameter value accepted");
        var result = default(TResult);
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectFromSourceAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var rowSet = default(RowSet);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count == 1)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                var boundStatement = ctx.ParameterValues[0] is not null
                    ? ps.Bind(ctx.ParameterValues[0])
                    : ps.Bind();
                rowSet = await session.ExecuteAsync(boundStatement);
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                rowSet = await session.ExecuteAsync(simpleStatement);
            }
            var row = rowSet.SingleOrDefault();
            if (row is not null)
            {
                var mapReader = new ScyllaDbObjectDataMapReader<TSource>(row);
                result = dataReaderMapper(mapReader);
            }
            return result!;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    public ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        throw new NotImplementedException();
    }

    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper, Action<IEnumerable<TResult>> dataReducer)
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.ExecuteMapReduceAsync: dataMapper parameter is null");
        if (dataReducer is null)
            throw new StorageException($"{ClassName}.ExecuteMapReduceAsync: dataReducer parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.ExecuteMapReduceAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectsAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count > 0)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                foreach (var bindValues in ctx.ParameterValues)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    using var rowSet = await session.ExecuteAsync(boundStatement);
                    dataReducer.Invoke(GetReducer(rowSet));
                }
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                using var rowSet = await session.ExecuteAsync(simpleStatement);
                dataReducer.Invoke(GetReducer(rowSet));
            }
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectsAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }

        IEnumerable<TResult> GetReducer(RowSet rowSet)
        {
            var record = rowSet.ToObjectDataRecord();
            foreach (var row in rowSet)
                yield return dataMapper(record.SetRow(row));
        }
    }

    /// <summary>
    /// Reads data from the specified <see cref="NpgsqlDataReader"/> and maps each row to a result of type <typeparamref
    /// name="TResult"/>.
    /// </summary>
    /// <remarks>This method processes all rows in the <paramref name="dataReader"/> and uses the provided
    /// <paramref name="dataMapper"/> function to transform each row into an instance of <typeparamref name="TResult"/>.
    /// The rows are represented as concatenated strings with column values separated by a pipe ("|")
    /// character.</remarks>
    /// <typeparam name="TResult">The type of the result produced by the <paramref name="dataMapper"/> function.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. Must be positioned before the first record.</param>
    /// <param name="dataMapper">A function that maps a string representation of a row and an index to an instance of <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>A <see cref="LinkedList{T}"/> containing the mapped results of type <typeparamref name="TResult"/> for each row
    /// in the data reader.</returns>
    [ThreadStatic] static StringBuilder? t_cachedStringBuilder;

    static List<TResult> GetResultSet<TResult>(RowSet rowSet, Func<string, int, TResult> dataMapper)
    {
        List<TResult> resultSet = [];
        var sb = t_cachedStringBuilder ??= new StringBuilder(256);
        foreach(var row in rowSet)
        {
            var index = 0;
            sb.Clear();
            foreach (var value in row)
            {
                if (index > 0)
                    sb.Append('|');
                sb.Append(value?.ToString() ?? string.Empty);
                index++;
            }
            var start = 0;
            resultSet.Add(dataMapper(sb.ToString(), start));
        }
        return resultSet;
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a collection using an <see cref="IObjectDataRecord"/>
    /// mapper, eliminating intermediate <c>object[]</c> allocation and value-type boxing.
    /// </summary>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.GetObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectsAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectsAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            List<TResult> resultSet = [];
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count > 0)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                foreach (var bindValues in ctx.ParameterValues)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    using var rowSet = await session.ExecuteAsync(boundStatement);
                    resultSet = GetResultSet(rowSet, dataMapper);
                }
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                using var rowSet = await session.ExecuteAsync(simpleStatement);
                resultSet = GetResultSet(rowSet, dataMapper);
            }
            return resultSet;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectsAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a pooled, read-only buffer of value types
    /// using <see cref="ScyllaDbResultSetMaterializer"/>, eliminating per-row heap allocations.
    /// </summary>
    public async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper) where TResult : struct
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.GetImmutableObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetImmutableObjectsAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetImmutableObjectsAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count > 0)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                foreach (var bindValues in ctx.ParameterValues)
                {
                    var boundStatement = bindValues is not null
                        ? ps.Bind(bindValues)
                        : ps.Bind();
                    using var rowSet = await session.ExecuteAsync(boundStatement);
                    return ScyllaDbResultSetMaterializer.GetResultSet(rowSet, dataMapper);
                }
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                using var rowSet = await session.ExecuteAsync(simpleStatement);
                return ScyllaDbResultSetMaterializer.GetResultSet(rowSet, dataMapper);
            }
            return new PooledReadOnlyBuffer<TResult>(System.Buffers.MemoryPool<TResult>.Shared.Rent(0), 0);
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetImmutableObjectsAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a query asynchronously and maps the first row to a single object using an
    /// <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.GetObjectAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.GetObjectAsync: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetObjectAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var rowSet = default(RowSet);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count == 1)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                var boundStatement = ctx.ParameterValues[0] is not null
                    ? ps.Bind(ctx.ParameterValues[0])
                    : ps.Bind();
                rowSet = await session.ExecuteAsync(boundStatement);
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                rowSet = await session.ExecuteAsync(simpleStatement);
            }
            return GetSingle(rowSet, dataMapper)!;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetObjectAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    /// <summary>
    /// Executes a scalar query asynchronously and maps the result using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.ExecuteScalar: only single parameter value accepted");
        try
        {
            _logger.LogInformationEvent(ClassName, "GetScalarAsync: {CommandText} with {ParameterValuesCount} parameter values", ctx.CommandText, ctx.ParameterValues.Count);
            var rowSet = default(RowSet);
            var session = await _conn.CreateSessionAsync();
            if (ctx.ParameterValues.Count == 1)
            {
                var ps = GetOrPrepare(session, ctx.CommandText);
                var boundStatement = ctx.ParameterValues[0] is not null
                    ? ps.Bind(ctx.ParameterValues[0])
                    : ps.Bind();
                rowSet = await session.ExecuteAsync(boundStatement);
            }
            else
            {
                var simpleStatement = new SimpleStatement(ctx.CommandText);
                rowSet = await session.ExecuteAsync(simpleStatement);
            }
            return GetScalar(rowSet, dataMapper);
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.GetScalarAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    // --- IObjectDataRecord-based helper methods (zero object[] allocation) ---

    static List<TResult> GetResultSet<TResult>(RowSet rowSet, Func<IObjectDataRecord, TResult> dataMapper)
    {
        List<TResult> resultSet = [];
        var record = rowSet.ToObjectDataRecord();
        foreach (var row in rowSet)
            resultSet.Add(dataMapper(record.SetRow(row)));
        return resultSet;
    }

    static TResult? GetSingle<TResult>(RowSet rowSet, Func<IObjectDataRecord, TResult> dataMapper)
    {
        var record = rowSet.ToObjectDataRecord();
        foreach (var row in rowSet)
            return dataMapper(record.SetRow(row));
        return default;
    }

    static TScalar GetScalar<TScalar>(RowSet rowSet, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct
    {
        var record = rowSet.ToObjectDataRecord();
        foreach (var row in rowSet)
            return dataMapper(record.SetRow(row));
        return default;
    }

}
