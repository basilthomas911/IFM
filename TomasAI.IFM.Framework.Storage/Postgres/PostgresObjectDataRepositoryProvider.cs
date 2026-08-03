using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.Postgres;

public class PostgresObjectDataRepositoryProvider : IObjectRepositoryProvider
{
    const string ProviderTypeName = "PostgresObjectDataRepositoryProvider";
    readonly IObjectRepositoryContext _ctx;

    static readonly Dictionary<Type, NpgsqlDbType> _dbTypeMap = new()
    {
        { typeof(string), NpgsqlDbType.Text },
        { typeof(int), NpgsqlDbType.Integer },
        { typeof(long), NpgsqlDbType.Bigint },
        { typeof(short), NpgsqlDbType.Smallint },
        { typeof(byte), NpgsqlDbType.Smallint },
        { typeof(bool), NpgsqlDbType.Boolean },
        { typeof(DateTime), NpgsqlDbType.Timestamp },
        { typeof(DateOnly), NpgsqlDbType.Date },
        { typeof(decimal), NpgsqlDbType.Money },
        { typeof(float), NpgsqlDbType.Real },
        { typeof(double), NpgsqlDbType.Double },
        { typeof(Guid), NpgsqlDbType.Uuid },
        { typeof(TimeSpan), NpgsqlDbType.Bigint },
        { typeof(byte[]), NpgsqlDbType.Bytea },
        { typeof(int?), NpgsqlDbType.Integer },
        { typeof(long?), NpgsqlDbType.Bigint },
        { typeof(short?), NpgsqlDbType.Smallint },
        { typeof(byte?), NpgsqlDbType.Smallint },
        { typeof(bool?), NpgsqlDbType.Boolean },
        { typeof(DateTime?), NpgsqlDbType.Timestamp },
        { typeof(DateOnly?), NpgsqlDbType.Date },
        { typeof(decimal?), NpgsqlDbType.Money },
        { typeof(float?), NpgsqlDbType.Real },
        { typeof(double?), NpgsqlDbType.Double },
        { typeof(Guid?), NpgsqlDbType.Uuid },
        { typeof(TimeSpan?), NpgsqlDbType.Bigint }
    };

    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = [];

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
            var dbParametersList = GetParameters().ToList();
            if (dbParametersList is not null && dbParametersList.Count > 0)
            {
                foreach (var dbParameters in dbParametersList)
                {
                    cmd.Parameters.Clear();
                    if (dbParameters is not null && dbParameters.Count() > 0)
                        foreach (var dbParameter in dbParameters)
                            cmd.Parameters.AddWithValue(dbParameter.NpgsqlDbType, dbParameter.NpgsqlValue);
                    if (cmd.CommandType == CommandType.StoredProcedure)
                    {
                        var returnParameter = cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, default);
                        returnParameter.Direction = ParameterDirection.Output;
                        affectedRows.Add(await cmd.ExecuteNonQueryAsync());
                    }
                    else
                        affectedRows.Add(await cmd.ExecuteNonQueryAsync());
                }
            }
            else if (cmd.CommandType == CommandType.StoredProcedure)
            {
                var returnParameter = cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, default);
                returnParameter.Direction = ParameterDirection.Output;
                affectedRows.Add(await cmd.ExecuteNonQueryAsync());
            }
            else
                affectedRows.Add(await cmd.ExecuteNonQueryAsync());
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
        var dbParameters = GetParameters(parameterValues).FirstOrDefault(); 
        return new ObjectDataQueuedCommand(commandType, commandText, dbParameters );
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
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            foreach (ObjectDataQueuedCommand queuedCommand in queuedCommands!)
            {
                if (queuedCommand is null) continue;
                if (string.IsNullOrWhiteSpace(queuedCommand.CommandText))
                    throw new ArgumentException($"{ProviderTypeName}.ExecuteQueuedCommandsAsync: command text parameter is empty");
                cmd.CommandType = queuedCommand.CommandType;
                cmd.CommandText = queuedCommand.CommandText;
                commandText = cmd.CommandText;
                cmd.Parameters.Clear();
                if (queuedCommand.Parameters is not null && queuedCommand.Parameters.Length > 0)
                    foreach (var spParameter in queuedCommand.Parameters)
                    {
                        var parameter = (NpgsqlParameter)spParameter;
                        cmd.Parameters.AddWithValue(parameter.NpgsqlDbType, parameter.NpgsqlValue);
                    }
                await cmd.ExecuteNonQueryAsync();
             }
             tx.Commit();
            conn.Close();
        }
        catch (Exception ex)
        {
            TryRollback(tx);
            conn.Close();
            while (ex.InnerException != null) ex = ex.InnerException;
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
    /// return list of db parameters from list of update objects
    /// </summary>
    /// <typeparam name="TParam"></typeparam>
    /// <param name="paramValues">list of update objects</param>
    /// <returns></returns>
    IEnumerable<NpgsqlParameter[]> GetParameters() => GetParameters(_ctx.ParameterValues);

    IEnumerable<NpgsqlParameter[]> GetParameters(List<object> values)
    {
        if (values.Count == 0) yield break;
        PropertyInfo[]? paramProps = null;
        foreach (var paramValue in values)
        {
            if (paramValue == null) continue;
            paramProps ??= _propertyCache.GetOrAdd(paramValue.GetType(), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            var dbParameters = new NpgsqlParameter[paramProps.Length];
            for (var i = 0; i < paramProps.Length; i++)
            {
                var propInfo = paramProps[i];
                var propValue = propInfo.GetValue(paramValue);
                var paramType = propValue is not null
                    ? GetDbTypeFromParameterValue(propValue)
                    : GetDbTypeFromParameterValue(propInfo.PropertyType);
                var dbParameter = new NpgsqlParameter(_ctx.GetParameterName(propInfo.Name), paramType);
                dbParameter.Value = propValue;
                dbParameter.Direction = ParameterDirection.Input;
                dbParameters[i] = dbParameter;
            }
            yield return dbParameters;
        }
    }

    /// <summary>
    /// return DbType from type of parameter value
    /// </summary>
    /// <param name="value">parameter value</param>
    /// <returns></returns>
    NpgsqlDbType GetDbTypeFromParameterValue(object value)
        => GetDbTypeFromParameterValue(value.GetType());

    /// <summary>
    /// return DbType from type of parameter value
    /// </summary>
    /// <param name="valueType">parameter value type</param>
    /// <returns></returns>
    NpgsqlDbType GetDbTypeFromParameterValue(Type parameterValueType)
    {
        if (_dbTypeMap.TryGetValue(parameterValueType, out var dbType))
            return dbType;
        throw new StorageException($"{ProviderTypeName}.GetDbTypeFromParameterValue: unknown value type: '{parameterValueType}'");
    }

    /// <summary>
    /// set parameter values
    /// </summary>
    /// <param name="cmd"></param>
    void SetParameters(NpgsqlCommand cmd)
    {
        cmd.Parameters.Clear();
        if (_ctx.ParameterValues.Count == 1)
        {
            foreach (var parameters in GetParameters())
            {
                foreach (var e in parameters)
                    cmd.Parameters.AddWithValue(e.NpgsqlDbType, e.NpgsqlValue);
                break;
            }
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
