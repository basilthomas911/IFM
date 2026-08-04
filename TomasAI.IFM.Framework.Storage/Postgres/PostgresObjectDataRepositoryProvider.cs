using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.Postgres;

public class PostgresObjectDataRepositoryProvider : IObjectRepositoryProvider
{
    const string ProviderTypeName = "PostgresObjectDataRepositoryProvider";
    readonly IObjectRepositoryContext _ctx;
    readonly PostgresBulkWriteOptions _bulkWriteOptions;
    readonly object _connectionIdentity;

    /// <summary>
    /// create postgres object data repository provider 
    /// </summary>
    /// <param name="ctx"></param>
    public PostgresObjectDataRepositoryProvider(IObjectRepositoryContext ctx, ILogger logger)
    {
        _ctx = ctx;
        _bulkWriteOptions = PostgresBulkWriteOptions.FromEnvironment();
        _connectionIdentity = RepositoryConnectionIdentity.Get(ctx.Repository);
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <returns></returns>
    public Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null)
        => ExecuteCommandAsync(ctx, CancellationToken.None, onInfoMessage);

    public async Task<long[]> ExecuteCommandAsync(
        IObjectRepositoryContext ctx,
        CancellationToken cancellationToken,
        Action<string> onInfoMessage = null)
    {
        var (commandText, commandType) = GetCommandDefinition(ctx);
        IEnumerator<object>? enumerator = null;
        var hasFirst = false;
        object? first = null;
        var hasSecond = false;
        object? second = null;
        NpgsqlConnection? ownedConnection = null;
        NpgsqlTransaction? ownedTransaction = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parameterValues = ctx is ObjectDataRepositoryContext repositoryContext
                ? repositoryContext.ReadParameterValues()
                : ctx.ParameterValues;
            enumerator = parameterValues.GetEnumerator();
            hasFirst = enumerator.MoveNext();
            first = hasFirst ? enumerator.Current : null;
            hasSecond = hasFirst && enumerator.MoveNext();
            second = hasSecond ? enumerator.Current : null;

            await using var ambientCommand = ctx.Repository.InTransaction() as NpgsqlCommand;
            if (ambientCommand is not null)
            {
                return await ExecuteCoreAsync(
                    ambientCommand.Connection!,
                    ambientCommand.Transaction,
                    hasFirst,
                    first,
                    hasSecond,
                    second).ConfigureAwait(false);
            }

            ownedConnection = ctx.Repository.CreateConnection()
                .As<NpgsqlConnection>(ctx.Repository.ConnectionString);
            await ownedConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // A single PostgreSQL statement is already atomic. Use an explicit transaction only when
            // several parameter payloads must retain the context's all-or-nothing contract.
            if (hasSecond && ctx.UseTransaction)
                ownedTransaction = await ownedConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var result = await ExecuteCoreAsync(
                ownedConnection,
                ownedTransaction,
                hasFirst,
                first,
                hasSecond,
                second).ConfigureAwait(false);
            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (ownedTransaction is not null)
                await TryRollbackAsync(ownedTransaction).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            if (ownedTransaction is not null)
                await TryRollbackAsync(ownedTransaction).ConfigureAwait(false);
            throw new StorageException(
                $"{ProviderTypeName}.ExecuteCommandAsync: {commandText} {ex.Message}",
                ex);
        }
        finally
        {
            enumerator?.Dispose();
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync().ConfigureAwait(false);
            if (ownedConnection is not null)
                await ownedConnection.DisposeAsync().ConfigureAwait(false);
        }

        async Task<long[]> ExecuteCoreAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction? transaction,
            bool hasFirstValue,
            object? firstValue,
            bool hasSecondValue,
            object? secondValue)
        {
            if (!hasFirstValue)
                return [await ExecuteSingleAsync(connection, transaction, null, false).ConfigureAwait(false)];
            if (!hasSecondValue)
                return [await ExecuteSingleAsync(connection, transaction, firstValue, true).ConfigureAwait(false)];

            var knownCount = ctx is ObjectDataRepositoryContext objectContext
                ? objectContext.ParameterValueCount
                : null;
            var affectedRows = knownCount is > 0
                ? new List<long>(knownCount.Value)
                : new List<long>();
            using var values = ReadValues().GetEnumerator();
            var hasValue = values.MoveNext();
            var executed = false;
            while (hasValue)
            {
                await using var batch = new NpgsqlBatch(connection, transaction);
                if (ctx.CommandTimeout > 0)
                    batch.Timeout = ctx.CommandTimeout;

                for (var index = 0; index < _bulkWriteOptions.BatchSize && hasValue; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var parameters = GetParameterArray(values.Current);
                    cancellationToken.ThrowIfCancellationRequested();
                    hasValue = values.MoveNext();
                    if (parameters is null)
                        continue;

                    var batchCommand = new NpgsqlBatchCommand(commandText)
                    {
                        CommandType = commandType
                    };
                    foreach (var parameter in parameters)
                        batchCommand.Parameters.Add(parameter);
                    AddStoredProcedureReturnParameter(batchCommand.Parameters, batchCommand.CommandType);
                    batch.BatchCommands.Add(batchCommand);
                }

                if (batch.BatchCommands.Count == 0)
                    continue;

                if (commandType == CommandType.Text)
                    await batch.PrepareAsync(cancellationToken).ConfigureAwait(false);
                await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                foreach (var batchCommand in batch.BatchCommands)
                    affectedRows.Add(batchCommand.RecordsAffected);
                executed = true;
            }

            if (!executed)
                affectedRows.Add(await ExecuteSingleAsync(connection, transaction, null, false).ConfigureAwait(false));
            return [.. affectedRows];

            IEnumerable<object?> ReadValues()
            {
                yield return firstValue;
                yield return secondValue;
                while (enumerator!.MoveNext())
                    yield return enumerator.Current;
            }
        }

        async Task<long> ExecuteSingleAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction? transaction,
            object? parameterValue,
            bool hasParameterValue)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (ctx.CommandTimeout > 0)
                command.CommandTimeout = ctx.CommandTimeout;
            ctx.SetCommand(command);
            if (command.CommandType == CommandType.StoredProcedure)
                command.CommandText = commandText;

            if (hasParameterValue)
            {
                var parameters = GetParameterArray(parameterValue);
                if (parameters is not null)
                {
                    foreach (var parameter in parameters)
                        command.Parameters.Add(parameter);
                }
            }

            AddStoredProcedureReturnParameter(command.Parameters, command.CommandType);
            await PrepareParameterizedCommandAsync(command, cancellationToken).ConfigureAwait(false);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    internal static (string CommandText, CommandType CommandType) GetCommandDefinition(
        IObjectRepositoryContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        string commandText;
        CommandType commandType;
        if (ctx is ObjectDataRepositoryContext objectContext)
        {
            commandText = ctx.CommandText;
            commandType = objectContext.GetCommandType();
        }
        else
        {
            // IObjectRepositoryContext is a public extension point. Preserve the
            // former provider behavior by obtaining command metadata through its
            // SetCommand contract when the framework's concrete context is not used.
            using var command = new NpgsqlCommand();
            ctx.SetCommand(command);
            commandText = command.CommandText;
            commandType = command.CommandType;
        }

        if (commandType != CommandType.StoredProcedure)
            return (commandText, commandType);

        var schema = !string.IsNullOrEmpty(ctx.Repository.Schema) ? ctx.Repository.Schema : "public";
        return ($"{schema}.{commandText}", commandType);
    }

    static void AddStoredProcedureReturnParameter(
        NpgsqlParameterCollection parameters,
        CommandType commandType)
    {
        if (commandType != CommandType.StoredProcedure)
            return;

        var returnParameter = parameters.AddWithValue(NpgsqlDbType.Integer, default);
        returnParameter.Direction = ParameterDirection.Output;
    }

    /// <summary>
    /// queue command for execution
    /// </summary>
    /// <param name="commandText"></param>
    /// <param name="commandType"></param>
    /// <param name="parameterValues"></param>
    /// <exception cref="ArgumentException"></exception>
    public object QueueCommand(string commandText, CommandType commandType, List<object> parameterValues)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException($"{ProviderTypeName}.QueueCommand: command text parameter is empty");
        NpgsqlParameter[]? dbParameters = null;
        foreach (var parameterValue in parameterValues)
        {
            dbParameters = GetParameterArray(parameterValue);
            if (dbParameters is not null)
                break;
        }

        return new ObjectDataQueuedCommand(
            commandType,
            commandText,
            dbParameters,
            _ctx.Repository.ProviderName,
            _connectionIdentity);
    }

    /// <summary>
    /// execute list of queued commands 
    /// </summary>
    /// <returns></returns>
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
    {
        if (queuedCommands?.Count == 0)
            throw new InvalidOperationException($"{ProviderTypeName}.ExecuteQueuedCommandsAsync: no commands have been queued for execution");
        var commandText = string.Empty;
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var tx = useTransaction
            ? await conn.BeginTransactionAsync().ConfigureAwait(false)
            : null;
        try
        {
            await using var batch = new NpgsqlBatch(conn, tx);
            if (_ctx.CommandTimeout > 0)
                batch.Timeout = _ctx.CommandTimeout;
            var prepareBatch = true;
            var hasParameterizedCommand = false;
            foreach (ObjectDataQueuedCommand queuedCommand in queuedCommands!)
            {
                if (queuedCommand is null) continue;
                if (string.IsNullOrWhiteSpace(queuedCommand.CommandText))
                    throw new ArgumentException($"{ProviderTypeName}.ExecuteQueuedCommandsAsync: command text parameter is empty");
                commandText = queuedCommand.CommandText;
                var batchCommand = new NpgsqlBatchCommand(commandText)
                {
                    CommandType = queuedCommand.CommandType
                };
                prepareBatch &= queuedCommand.CommandType == CommandType.Text;
                if (queuedCommand.Parameters is not null && queuedCommand.Parameters.Length > 0)
                {
                    hasParameterizedCommand = true;
                    foreach (var spParameter in queuedCommand.Parameters)
                        batchCommand.Parameters.Add((NpgsqlParameter)spParameter);
                }

                batch.BatchCommands.Add(batchCommand);
            }

            if (batch.BatchCommands.Count > 0)
            {
                commandText = "NpgsqlBatch";
                if (prepareBatch && hasParameterizedCommand)
                    await batch.PrepareAsync().ConfigureAwait(false);
                await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            if (tx is not null)
                await tx.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (tx is not null)
                await TryRollbackAsync(tx).ConfigureAwait(false);
            while (ex.InnerException != null) ex = ex.InnerException;
            if (ex is NpgsqlException { BatchCommand: not null } npgsqlException)
                commandText = npgsqlException.BatchCommand.CommandText;
            var errorMessage = $"{ProviderTypeName}.ExecuteQueuedCommandAsync: {commandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    static async Task TryRollbackAsync(NpgsqlTransaction transaction)
    {
        try
        {
            if (transaction.Connection is not null)
                await transaction.RollbackAsync().ConfigureAwait(false);
        }
        catch
        {
            // Preserve the database exception that caused the rollback attempt.
        }
    }


    /// <summary>
    /// execute bulk insert directly into sql server database
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="sourceDataReader"></param>
    public void BulkCopy()
    {
        throw new NotImplementedException($"{ProviderTypeName}.BulkCopy: not implemented");
        /*
        using (var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString))
        {
            conn.Open();
            try
            {
                using (var bulkCopy = new NpgsqlBulkCopy(conn as NpgsqlConnection))
                {
                    var bulkInsertParameters = GetBulkInsertParameters();
                    bulkCopy.DestinationTableName = bulkInsertParameters.tableName;
                    var sourceDataReader = bulkInsertParameters.sourceDataReader;
                    for (var ordinal = 0; ordinal < sourceDataReader.FieldCount; ordinal++)
                    {
                        var columnName = sourceDataReader.GetName(ordinal);
                        bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping
                        {
                            DestinationColumn = columnName,
                            SourceColumn = columnName
                        });
                    }
                    bulkCopy.WriteToServer(sourceDataReader);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"SqlServerObjectRepositoryProvider.BulkCopy: {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
            conn.Close();
        }
        */
    }


    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.ExecuteMapReduceAsync: only single parameter value accepted");
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        reducer?.Invoke(MapReduce());

        IEnumerable<TResult> MapReduce()
        {
            while (dataReader.Read())
                yield return mapper(record);
        }
    }
    /// <summary>
    /// Validates and returns an already-created positional parameter array.
    /// </summary>
    /// <param name="value">provider bind payload</param>
    /// <returns></returns>
    static NpgsqlParameter[]? GetParameterArray(object? value)
    {
        if (value is null)
            return null;
        if (value is NpgsqlParameter[] parameters)
            return parameters;

        throw new StorageException(
            $"{ProviderTypeName}.GetParameterArray: expected an NpgsqlParameter[] positional payload but received '{value.GetType()}'.");
    }

    static Task PrepareParameterizedCommandAsync(
        NpgsqlCommand command,
        CancellationToken cancellationToken = default)
        => command.CommandType == CommandType.Text && command.Parameters.Count > 0
            ? command.PrepareAsync(cancellationToken)
            : Task.CompletedTask;

    /// <summary>
    /// set parameter values
    /// </summary>
    /// <param name="cmd"></param>
    void SetParameters(NpgsqlCommand cmd)
    {
        cmd.Parameters.Clear();
        if (_ctx.ParameterValues.Count == 1)
        {
            var parameters = GetParameterArray(_ctx.ParameterValues[0]);
            if (parameters is not null)
                foreach (var e in parameters)
                    cmd.Parameters.Add(e);
        }
    }

    /// <summary>
    /// Asynchronously streams mapped rows while keeping the connection and reader scoped to the enumerator.
    /// Disposing the enumerator releases both resources, including when enumeration stops early.
    /// </summary>
    public async IAsyncEnumerable<TResult> StreamObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> dataMapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.StreamObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.StreamObjectsAsync: only single parameter value accepted");

        cancellationToken.ThrowIfCancellationRequested();
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd, cancellationToken).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(
            CommandBehavior.CloseConnection,
            cancellationToken).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                yield break;

            yield return dataMapper(record);
        }
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a collection using an <see cref="IObjectDataRecord"/>
    /// mapper, eliminating intermediate <c>object[]</c> allocation and value-type boxing.
    /// </summary>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        List<TResult> resultSet = [];
        while (await dataReader.ReadAsync().ConfigureAwait(false))
            resultSet.Add(dataMapper(record));
        return resultSet;
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a read-only list using an <see cref="IObjectDataRecord"/>
    /// mapper, returning an immutable collection of value types.
    /// </summary>
    public async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper) where TResult : struct
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.GetImmutableObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetImmutableObjectsAsync: only single parameter value accepted");
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        var builder = new PooledBufferBuilder<TResult>(capacity: 16);
        try
        {
            while (await dataReader.ReadAsync().ConfigureAwait(false))
                builder.Add(dataMapper(record));
            // IReadOnlyList does not communicate ownership or disposal. Return an
            // application-owned exact array after using pooled memory only as the
            // temporary growth buffer.
            return builder.MoveToArray();
        }
        catch
        {
            builder.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Executes a query asynchronously and maps the first row to a single object using an
    /// <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: dataMapper parameter is null");
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        if (await dataReader.ReadAsync().ConfigureAwait(false))
            return dataMapper(record);
        return default;
    }

    /// <summary>
    /// Executes a scalar query asynchronously and maps the result using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetScalarAsync: only single parameter value accepted");
        await using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd).ConfigureAwait(false);
        await using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        if (await dataReader.ReadAsync().ConfigureAwait(false))
            return dataMapper(record);
        return default;
    }

    public void Dispose()
    {
    }


}
