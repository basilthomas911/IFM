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

    /// <summary>
    /// create postgres object data repository provider 
    /// </summary>
    /// <param name="ctx"></param>
    public PostgresObjectDataRepositoryProvider(IObjectRepositoryContext ctx, ILogger logger)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <returns></returns>
    public async Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null)
    {
        var cmd = _ctx.Repository.InTransaction();
        if (cmd is not null)
            return await ExecuteSqlCommandAsync(cmd as NpgsqlCommand);
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        var affectedRows = _ctx.UseTransaction
            ? await UseTransactionAsync(conn)
            : await UseNoTransactionAsync(conn);
        conn.Close();
        return affectedRows;

        async Task<long[]> UseTransactionAsync(NpgsqlConnection conn)
        {
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            try
            {
                cmd.Transaction = tx;
                var result = await ExecuteSqlCommandAsync(cmd);
                tx.Commit();
                return result;
            }
            catch (Exception ex)
            {
                TryRollback(tx);
                var errorMessage = $"{ProviderTypeName}.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }

        async Task<long[]> UseNoTransactionAsync(NpgsqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            try
            {
                return await ExecuteSqlCommandAsync(cmd);
            }
            catch (Exception ex)
            {
                var errorMessage = $"{ProviderTypeName}.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }

        async Task<long[]> ExecuteSqlCommandAsync(NpgsqlCommand cmd)
        {
            List<long> affectedRows = [];
            if (_ctx.CommandTimeout > 0)
                cmd.CommandTimeout = _ctx.CommandTimeout;
            _ctx.SetCommand(cmd);
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                var schema = !string.IsNullOrEmpty(_ctx.Repository.Schema) ? _ctx.Repository.Schema : "public";
                cmd.CommandText = $"{schema}.{cmd.CommandText}";
            }
            var executed = false;
            foreach (var parameterValue in _ctx.ParameterValues)
            {
                var dbParameters = GetParameterArray(parameterValue);
                if (dbParameters is null)
                    continue;

                cmd.Parameters.Clear();
                foreach (var dbParameter in dbParameters)
                    cmd.Parameters.Add(dbParameter);
                if (cmd.CommandType == CommandType.StoredProcedure)
                {
                    var returnParameter = cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, default);
                    returnParameter.Direction = ParameterDirection.Output;
                }

                await PrepareParameterizedCommandAsync(cmd);
                affectedRows.Add(await cmd.ExecuteNonQueryAsync());
                executed = true;
            }

            if (!executed)
            {
                if (cmd.CommandType == CommandType.StoredProcedure)
                {
                    var returnParameter = cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, default);
                    returnParameter.Direction = ParameterDirection.Output;
                }

                affectedRows.Add(await cmd.ExecuteNonQueryAsync());
            }

            return [.. affectedRows];
        }
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

        return new ObjectDataQueuedCommand(commandType, commandText, dbParameters);
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
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
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
                    await batch.PrepareAsync();
                await batch.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            TryRollback(tx);
            while (ex.InnerException != null) ex = ex.InnerException;
            if (ex is NpgsqlException { BatchCommand: not null } npgsqlException)
                commandText = npgsqlException.BatchCommand.CommandText;
            var errorMessage = $"{ProviderTypeName}.ExecuteQueuedCommandAsync: {commandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
        }
    }

    static void TryRollback(NpgsqlTransaction transaction)
    {
        try
        {
            if (transaction.Connection is not null)
                transaction.Rollback();
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        List<TResult> resultSet = [];
        while (dataReader.Read())
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        List<TResult> resultSet = [];
        while (dataReader.Read())
            resultSet.Add(dataMapper(record));
        return resultSet;
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        if (dataReader.Read())
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        await PrepareParameterizedCommandAsync(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        if (dataReader.Read())
            return dataMapper(record);
        return default;
    }

    public void Dispose()
    {
    }


}
