using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.SqlServer;

public class SqlServerObjectDataRepositoryProvider : IObjectRepositoryProvider
{
    static readonly Dictionary<Type, DbType> _dbTypeMap = new()
    {
        {typeof(string), DbType.String },
        {typeof(bool), DbType.Boolean },
        {typeof(bool?), DbType.Boolean },
        {typeof(int), DbType.Int32 },
        {typeof(int?), DbType.Int32 },
        {typeof(short), DbType.Int16 },
        {typeof(short?), DbType.Int16 },
        {typeof(long), DbType.Int64 },
        {typeof(long?), DbType.Int64 },
        {typeof(double), DbType.Double },
        {typeof(double?), DbType.Double },
        {typeof(float), DbType.Double },
        {typeof(float?), DbType.Double },
        {typeof(decimal), DbType.Decimal },
        {typeof(decimal?), DbType.Decimal },
        {typeof(DateTime), DbType.DateTime },
        {typeof(DateTime?), DbType.DateTime },
        {typeof(TimeSpan), DbType.String },
        {typeof(TimeSpan?), DbType.String },
        {typeof(Guid), DbType.Guid },
        {typeof(Guid?), DbType.Guid },
        {typeof(ulong), DbType.Int64 },
        {typeof(ulong?), DbType.Int64 },
        {typeof(byte[]), DbType.Binary}
    };
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = [];
    readonly IObjectRepositoryContext _ctx;
    readonly object _connectionIdentity;

    public SqlServerObjectDataRepositoryProvider(IObjectRepositoryContext ctx, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _ctx = ctx;
        _connectionIdentity = RepositoryConnectionIdentity.Get(ctx.Repository);
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Validation and persistence orchestration remain together to preserve execution ordering.")]
    public async Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx, Action<string> onInfoMessage = null)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var status = new List<long>();
        var cmd = _ctx.Repository.InTransaction() as SqlCommand;
        if (cmd is not null)
        {
            await (ExecuteSqlCommandAsync(cmd)).ConfigureAwait(false);
            return status.ToArray();
        }
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            if (onInfoMessage is null)
                await (conn.OpenAsync()).ConfigureAwait(false);
            else
            {
                conn.FireInfoMessageEventOnUserErrors = true;
                await (conn.OpenAsync()).ConfigureAwait(false);
                conn.InfoMessage += (sender, e) => onInfoMessage(e.Message);
            }
            if (_ctx.UseTransaction)
                await (UseTransactionAsync(conn)).ConfigureAwait(false);
            else
                await (UseNoTransactionAsync(conn)).ConfigureAwait(false);
            await conn.CloseAsync().ConfigureAwait(false);
        }
        return status.ToArray();

        async Task UseTransactionAsync(SqlConnection conn)
        {
            using (var tx = (SqlTransaction)await conn.BeginTransactionAsync().ConfigureAwait(false))
            {
                var cmd = default(SqlCommand);
                try
                {
                    using (cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        await (ExecuteSqlCommandAsync(cmd)).ConfigureAwait(false);
                    }
                    await tx.CommitAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                    var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteCommandAsync: {ctx.CommandLogText} {ex.Message}";
                    throw new StorageException(errorMessage, ex);
                }
            }
        }

        async Task UseNoTransactionAsync(SqlConnection conn)
        {
            var cmd = default(SqlCommand);
            try
            {
                using (cmd = conn.CreateCommand())
                {
                    await (ExecuteSqlCommandAsync(cmd)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteCommandAsync: {ctx.CommandLogText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }

        async Task ExecuteSqlCommandAsync(DbCommand cmd)
        {
            if (_ctx.CommandTimeout > 0)
                cmd.CommandTimeout = _ctx.CommandTimeout;
            _ctx.SetCommand(cmd);
            var dbParametersList = GetParameters().ToList();
            if (dbParametersList is not null && dbParametersList.Count > 0)
            {
                foreach (var dbParameters in dbParametersList)
                {
                    cmd.Parameters.Clear();
                    if (dbParameters is not null && dbParameters.Length > 0)
                        foreach (var dbParameter in dbParameters)
                            cmd.Parameters.Add(dbParameter);
                    var returnParameter = new SqlParameter("@ReturnVal", SqlDbType.BigInt);
                    returnParameter.Direction = ParameterDirection.ReturnValue;
                    cmd.Parameters.Add(returnParameter);
                    await (cmd.ExecuteNonQueryAsync()).ConfigureAwait(false);
                    status.Add(Convert.ToInt64(returnParameter.Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            else
            {
                var returnParameter = new SqlParameter("@ReturnVal", SqlDbType.BigInt);
                returnParameter.Direction = ParameterDirection.ReturnValue;
                cmd.Parameters.Add(returnParameter);
                await (cmd.ExecuteNonQueryAsync()).ConfigureAwait(false);
                status.Add(Convert.ToInt64(returnParameter.Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    public object QueueCommand(
        string commandName,
        string commandText,
        CommandType commandType,
        List<object> parameterValues)
    {
        ArgumentNullException.ThrowIfNull(parameterValues);
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("ObjectDataRepository.QueueCommand: command name parameter is empty",nameof(commandName));
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("ObjectDataRepository.QueueCommand: command text parameter is empty",nameof(commandText));
        var dbParameters = GetParameters(parameterValues).FirstOrDefault();
        return new ObjectDataQueuedCommand(
            commandName,
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "Repository infrastructure executes caller-defined SQL templates with values supplied through bound command parameters; identifiers are trusted schema definitions.")]
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
    {
        ArgumentNullException.ThrowIfNull(queuedCommands);
        if (queuedCommands.Count == 0)
            throw new InvalidOperationException("SqlServerObjectRepositoryProvider.ExecuteQueuedCommandsAsync: no commands have been queued for execution");
        var commandLogText = string.Empty;
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await (conn.OpenAsync()).ConfigureAwait(false);
            using (var tx = (SqlTransaction)await conn.BeginTransactionAsync().ConfigureAwait(false))
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {

                        cmd.Transaction = tx;
                        foreach (ObjectDataQueuedCommand queuedCommand in queuedCommands)
                        {
                            if (string.IsNullOrWhiteSpace(queuedCommand.CommandText))
                                throw new ArgumentException("SqlServerObjectRepositoryProvider.ExecuteQueuedCommandsAsync: command text parameter is empty",nameof(queuedCommands));
                            cmd.CommandType = queuedCommand.CommandType;
                            cmd.CommandText = queuedCommand.CommandText;
                            commandLogText = queuedCommand.CommandLogText;
                            cmd.Parameters.Clear();
                            if (queuedCommand.Parameters != null && queuedCommand.Parameters.Length > 0)
                                foreach (var spParameter in queuedCommand.Parameters)
                                    cmd.Parameters.Add(spParameter);
                            await (cmd.ExecuteNonQueryAsync()).ConfigureAwait(false);
                        }
                    }
                    await tx.CommitAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync().ConfigureAwait(false);
                    while (ex.InnerException != null) ex = ex.InnerException;
                    var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteQueuedCommandAsync: {commandLogText} {ex.Message}";
                    throw new StorageException(errorMessage, ex);
                }
            }
            await conn.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// execute bulk insert directly into sql server database
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="sourceDataReader"></param>
    public void BulkCopy()
    {
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            conn.Open();
            try
            {
                using (var bulkCopy = new SqlBulkCopy(conn))
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
    }


    /// <summary>
    /// return list of db parameters from list of update objects
    /// </summary>
    /// <typeparam name="TParam"></typeparam>
    /// <param name="paramValues">list of update objects</param>
    /// <returns></returns>

    IEnumerable<DbParameter[]> GetParameters()
        => GetParameters(_ctx.ParameterValues);

    IEnumerable<DbParameter[]> GetParameters(List<object> values)
    {
        if (values.Count == 0) yield break;
        PropertyInfo[]? paramProps = null;
        foreach (var paramValue in values)
        {
            if (paramValue == null) continue;
            paramProps ??= _propertyCache.GetOrAdd(paramValue.GetType(), t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
            var dbParameters = new DbParameter[paramProps.Length];
            for (var i = 0; i < paramProps.Length; i++)
            {
                var propInfo = paramProps[i];
                var dbParameter = _ctx.Repository.CreateParameter();
                dbParameter.ParameterName = _ctx.GetParameterName(propInfo.Name);
                var propValue = propInfo.GetValue(paramValue);
                if (propValue != null)
                    dbParameter.DbType = GetDbTypeFromParameterValue(propValue);
                else
                    dbParameter.DbType = GetDbTypeFromParameterValue(propInfo.PropertyType);
                dbParameter.Value = propValue;
                dbParameters[i] = dbParameter;
            }
            yield return dbParameters;
        }
    }

    /// <summary>
    /// return bulk insert parameters
    /// </summary>
    /// <returns></returns>
    (string tableName, IDataReader sourceDataReader) GetBulkInsertParameters()
    {
        var bulkInsertParameters = (tableName: default(string), sourceDataReader: default(IDataReader));
        if (_ctx.ParameterValues.Count == 1)
        {
            var paramProps = default(PropertyInfo[]);
            foreach (var paramValue in _ctx.ParameterValues)
            {
                if (paramValue == null) continue;
                paramProps = paramProps ?? paramValue.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var propInfo in paramProps)
                {
                    var propValue = propInfo.GetValue(paramValue);
                    switch (propInfo.Name)
                    {
                        case "tableName":
                            bulkInsertParameters.tableName = Convert.ToString(propValue, System.Globalization.CultureInfo.InvariantCulture);
                            break;
                        case "sourceDataReader":
                            bulkInsertParameters.sourceDataReader = propValue as IDataReader;
                            break;
                    }
                }
            }
        }
        return bulkInsertParameters;

    }

    /// <summary>
    /// return DbType from type of parameter value
    /// </summary>
    /// <param name="value">parameter value</param>
    /// <returns></returns>
    static DbType GetDbTypeFromParameterValue(object value)
    {
        var valueType = value.GetType();
        return GetDbTypeFromParameterValue(valueType);
    }

    /// <summary>
    /// return DbType from type of parameter value
    /// </summary>
    /// <param name="valueType">parameter value type</param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserves the existing instance/interface API used by dependency-injected callers.")]
    static DbType GetDbTypeFromParameterValue(Type parameterValueType)
    {
        if (_dbTypeMap.TryGetValue(parameterValueType, out var dbType))
            return dbType;
        throw new StorageException($"SqlServerObjectRepositoryProvider.GetDbTypeFromParameterValue: unknown parameter value type: '{parameterValueType}'");
    }

    /// <summary>
    /// set parameter values
    /// </summary>
    /// <param name="cmd"></param>
    void SetParameters(DbCommand cmd)
    {
        cmd.Parameters.Clear();
        if (_ctx.ParameterValues.Count == 1)
        {
            var parameters = GetParameters().FirstOrDefault();
            if (parameters is not null)
                foreach (var parameter in parameters)
                    cmd.Parameters.Add(parameter);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2953", Justification = "Retains the existing provider cleanup API; operations own and dispose their resources within each call.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Preserves the existing instance/interface API used by dependency-injected callers.")]
    public void Dispose()
    {
        // Provider owns no persistent resource; each operation disposes its connection and command.
    }

    /// <summary>
    /// Asynchronously streams mapped rows while keeping the connection and reader scoped to the enumerator.
    /// Disposing the enumerator releases both resources, including when enumeration stops early.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4456", Justification = "Async enumeration intentionally validates at enumeration start, before opening or scheduling any provider operation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
    public async IAsyncEnumerable<TResult> StreamObjectsAsync<TResult>(
        IObjectRepositoryContext ctx,
        Func<IObjectDataRecord, TResult> dataMapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (dataMapper is null)
            throw new StorageException("SqlServerObjectRepositoryProvider.StreamObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException("SqlServerObjectRepositoryProvider.StreamObjectsAsync: only single parameter value accepted");

        cancellationToken.ThrowIfCancellationRequested();
        var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await using var connDisposal = conn.ConfigureAwait(false);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        var cmd = conn.CreateCommand();
        await using var cmdDisposal = cmd.ConfigureAwait(false);
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        var dataReader = await cmd.ExecuteReaderAsync(
            CommandBehavior.CloseConnection,
            cancellationToken).ConfigureAwait(false);
        await using var dataReaderDisposal = dataReader.ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);

        while (await dataReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return dataMapper(record);
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a collection using an <see cref="IObjectDataRecord"/>
    /// mapper, eliminating intermediate <c>object[]</c> allocation and value-type boxing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (dataMapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await (conn.OpenAsync()).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await (cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)).ConfigureAwait(false);
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserves the existing concrete named-argument API.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserves the existing concrete named-argument API.")]
    public async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper) where TResult : struct
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (dataMapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await (conn.OpenAsync()).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await (cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        List<TResult> resultSet = [];
        while (await dataReader.ReadAsync().ConfigureAwait(false))
            resultSet.Add(dataMapper(record));
        return resultSet;
    }

    /// <summary>
    /// Executes a query asynchronously and maps the first row to a single object using an
    /// <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (dataMapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await (conn.OpenAsync()).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await (cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(dataMapper);
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetScalarAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await (conn.OpenAsync()).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await (cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        if (await dataReader.ReadAsync().ConfigureAwait(false))
            return dataMapper(record);
        return default;
    }

    /// <summary>
    /// Executes a map-reduce operation using an <see cref="IObjectDataRecord"/> mapper with yield-return streaming.
    /// </summary>
    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (mapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: mapper parameter is null");
        if (reducer is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: reducer parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await (conn.OpenAsync()).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await (cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)).ConfigureAwait(false);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        reducer.Invoke(MapReduce());

        IEnumerable<TResult> MapReduce()
        {
            while (dataReader.Read())
                yield return mapper(record);
        }
    }

}
