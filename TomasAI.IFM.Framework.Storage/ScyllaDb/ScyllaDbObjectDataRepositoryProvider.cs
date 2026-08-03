using Cassandra;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
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
    readonly ScyllaDbBulkWriteOptions _bulkWriteOptions;
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
        _bulkWriteOptions = ScyllaDbBulkWriteOptions.FromEnvironment();
    }

    /// <summary>
    /// Returns a cached <see cref="PreparedStatement"/> for the given CQL text, or prepares and caches one
    /// on first access. Eliminates repeated <c>session.Prepare()</c> round-trips for the same query.
    /// </summary>
    PreparedStatement GetOrPrepare(ISession session, string cql)
        => _preparedStatementCache.GetOrAdd(cql, static (key, s) => s.Prepare(key), session);

    /// <summary>
    /// Binds positional values directly to the prepared statement. ScyllaDB parameter catalogs emit
    /// <see cref="object"/> arrays in CQL marker order, avoiding reflection and property-map allocation.
    /// </summary>
    static BoundStatement Bind(PreparedStatement statement, object? parameterValue)
    {
        if (parameterValue is null)
            return statement.Bind();
        if (parameterValue is object[] values)
            return statement.Bind(values);

        return statement.Bind(parameterValue);
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public async Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null!)
        => await ExecuteCommandAsync(ctx, CancellationToken.None, onInfoMessage).ConfigureAwait(false);

    /// <summary>
    /// Executes a ScyllaDB command with bounded multi-row scheduling and cooperative cancellation.
    /// Cassandra driver 3.x requests cannot be cancelled after submission, so cancellation prevents new writes and
    /// stops awaiting in-flight requests while the driver may complete those requests in the background.
    /// </summary>
    public async Task<long[]> ExecuteCommandAsync(
        IObjectRepositoryContext ctx,
        CancellationToken cancellationToken,
        Action<string> onInfoMessage = null!)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameterCount = ctx is ObjectDataRepositoryContext repositoryContext
                ? repositoryContext.ParameterValueCount
                : ctx.ParameterValues.Count;
            _logger.LogDebug(
                "{ClassName}.ExecuteCommandAsync: {CommandText} with {ParameterValuesCount} parameter values",
                ClassName,
                ctx.CommandText,
                parameterCount ?? -1);

            var session = await _conn.CreateSessionAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            if (ctx is ObjectDataRepositoryContext deferredContext && deferredContext.HasDeferredParameterValues)
            {
                await ExecuteDeferredCommandsAsync(
                    session,
                    deferredContext.ReadParameterValues(),
                    deferredContext.ParameterValueCount).ConfigureAwait(false);
            }
            else
            {
                var parameterValues = ctx.ParameterValues;
                if (parameterValues.Count > 1)
                    await ExecuteIndexedCommandsAsync(session, parameterValues).ConfigureAwait(false);
                else if (parameterValues.Count == 1)
                    await ExecuteSingleCommandAsync(session, parameterValues[0]).ConfigureAwait(false);
                else
                {
                    using var rowSet = await session.ExecuteAsync(new SimpleStatement(ctx.CommandText))
                        .WaitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            return [-1L];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            var errorMessage = $"{ClassName}.ExecuteCommandAsync: {ctx.CommandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }

        async Task ExecuteSingleCommandAsync(ISession session, object bindValues)
        {
            var ps = GetOrPrepare(session, ctx.CommandText);
            var boundStatement = Bind(ps, bindValues);
            using var rowSet = await session.ExecuteAsync(boundStatement)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        async Task ExecuteIndexedCommandsAsync(ISession session, IReadOnlyList<object> parameterValues)
        {
            var preparedStatement = GetOrPrepare(session, ctx.CommandText);
            var nextIndex = -1;
            ExceptionDispatchInfo? failure = null;
            var workerCount = Math.Min(_bulkWriteOptions.MaxConcurrency, parameterValues.Count);
            var workers = new Task[workerCount];

            for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
                workers[workerIndex] = ExecuteWorkerAsync();

            await Task.WhenAll(workers).ConfigureAwait(false);
            failure?.Throw();

            async Task ExecuteWorkerAsync()
            {
                while (Volatile.Read(ref failure) is null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var index = Interlocked.Increment(ref nextIndex);
                    if (index >= parameterValues.Count)
                        return;

                    try
                    {
                        using var rowSet = await session.ExecuteAsync(Bind(preparedStatement, parameterValues[index]))
                            .WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(ex), null);
                        return;
                    }
                }
            }
        }

        async Task ExecuteDeferredCommandsAsync(ISession session, IEnumerable<object> parameterValues, int? knownCount)
        {
            using var enumerator = parameterValues.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                using var rowSet = await session.ExecuteAsync(new SimpleStatement(ctx.CommandText))
                    .WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var first = enumerator.Current;
            if (!enumerator.MoveNext())
            {
                await ExecuteSingleCommandAsync(session, first).ConfigureAwait(false);
                return;
            }

            var second = enumerator.Current;
            var preparedStatement = GetOrPrepare(session, ctx.CommandText);
            var channel = Channel.CreateBounded<object>(new BoundedChannelOptions(_bulkWriteOptions.BoundedCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false,
                AllowSynchronousContinuations = false
            });
            using var stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ExceptionDispatchInfo? failure = null;
            var workerCount = Math.Min(
                _bulkWriteOptions.MaxConcurrency,
                Math.Max(2, knownCount ?? _bulkWriteOptions.MaxConcurrency));
            var tasks = new Task[workerCount + 1];

            for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
                tasks[workerIndex] = ExecuteWorkerAsync();

            tasks[^1] = ProduceAsync();
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (failure is not null)
            {
                // The first database or enumeration failure is rethrown below.
            }
            failure?.Throw();

            async Task ProduceAsync()
            {
                try
                {
                    await channel.Writer.WriteAsync(first, stopSource.Token).ConfigureAwait(false);
                    await channel.Writer.WriteAsync(second, stopSource.Token).ConfigureAwait(false);
                    while (enumerator.MoveNext())
                        await channel.Writer.WriteAsync(enumerator.Current, stopSource.Token).ConfigureAwait(false);
                    channel.Writer.TryComplete();
                }
                catch (OperationCanceledException) when (failure is not null)
                {
                    channel.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(ex), null);
                    stopSource.Cancel();
                    channel.Writer.TryComplete(ex);
                }
            }

            async Task ExecuteWorkerAsync()
            {
                try
                {
                    await foreach (var bindValues in channel.Reader.ReadAllAsync(stopSource.Token).ConfigureAwait(false))
                    {
                        using var rowSet = await session.ExecuteAsync(Bind(preparedStatement, bindValues))
                            .WaitAsync(stopSource.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (failure is not null)
                {
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(ex), null);
                    stopSource.Cancel();
                    channel.Writer.TryComplete(ex);
                }
            }
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
                _logger.LogDebug("{ClassName}.ExecuteQueuedCommandsAsync: {CommandText} with {BindValuesCount} bind values", ClassName, cmd.CommandText, cmd.BindValues?.Count ?? 0);
                commandText = cmd.CommandText;
                if (cmd.BindValues!.Count > 0)
                {
                    var ps = GetOrPrepare(session, commandText);
                    foreach (var bindValues in cmd.BindValues)
                    {
                        var boundStatement = Bind(ps, bindValues);
                        using var rowSet = await session.ExecuteAsync(boundStatement).ConfigureAwait(false);
                    }
                }
                else
                {
                    var simpleStatement = new SimpleStatement(commandText);
                    using var rowSet = await session.ExecuteAsync(simpleStatement).ConfigureAwait(false);
                }
            }
        }

        async Task ExecuteQueuedCommandsAsBatchAsync(ISession session)
        {
            var batchStatement = new BatchStatement();
            batchStatement.SetBatchType(BatchType.Logged);
            var statementCount = 0;
            foreach (ScyllaDbObjectDataQueuedCommand cmd in queuedCommands.Cast<ScyllaDbObjectDataQueuedCommand>())
            {
                _logger.LogDebug("{ClassName}.ExecuteQueuedCommandsAsync: {CommandText} with {BindValuesCount} bind values", ClassName, cmd.CommandText, cmd.BindValues?.Count ?? 0);
                var ps = GetOrPrepare(session, cmd.CommandText);
                if (cmd.BindValues is { Count: > 0 })
                {
                    foreach (var bindValues in cmd.BindValues)
                    {
                        batchStatement.Add(Bind(ps, bindValues));
                        statementCount++;
                    }
                }
                else
                {
                    batchStatement.Add(Bind(ps, null));
                    statementCount++;
                }
            }
            if (statementCount > 50)
            {
                _logger.LogWarning(
                    "{ClassName}.ExecuteQueuedCommandsAsync is executing an explicit logged batch containing {StatementCount} statements; keep atomic batches small and partition-local where possible",
                    ClassName,
                    statementCount);
            }
            using var rowSet = await session.ExecuteAsync(batchStatement).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
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
                    var boundStatement = Bind(ps, bindValues);
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
    /// Asynchronously streams mapped rows and explicitly fetches ScyllaDB result pages without synchronous auto-paging.
    /// Disposing the enumerator releases the active row set, including when enumeration stops early.
    /// </summary>
    public async IAsyncEnumerable<TResult> StreamObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> dataMapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (dataMapper is null)
            throw new StorageException($"{ClassName}.StreamObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ClassName}.StreamObjectsAsync: only single parameter value accepted");

        cancellationToken.ThrowIfCancellationRequested();
        var session = await _conn.CreateSessionAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        IStatement statement;
        if (ctx.ParameterValues.Count == 1)
        {
            var preparedStatement = GetOrPrepare(session, ctx.CommandText);
            statement = Bind(preparedStatement, ctx.ParameterValues[0]);
        }
        else
        {
            statement = new SimpleStatement(ctx.CommandText);
        }
        statement.SetAutoPage(false);

        using var rowSet = await session.ExecuteAsync(statement).WaitAsync(cancellationToken).ConfigureAwait(false);
        var record = rowSet.ToObjectDataRecord();
        while (true)
        {
            using var rows = rowSet.GetEnumerator();
            while (rows.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return dataMapper(record.SetRow(rows.Current));
            }

            if (rowSet.IsFullyFetched)
                yield break;

            await rowSet.FetchMoreResultsAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
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
                    var boundStatement = Bind(ps, bindValues);
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
                    var boundStatement = Bind(ps, bindValues);
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
                var boundStatement = Bind(ps, ctx.ParameterValues[0]);
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
                var boundStatement = Bind(ps, ctx.ParameterValues[0]);
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
