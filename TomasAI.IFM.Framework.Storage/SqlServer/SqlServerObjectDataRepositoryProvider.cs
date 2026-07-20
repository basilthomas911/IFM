using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
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

    public SqlServerObjectDataRepositoryProvider(IObjectRepositoryContext ctx, ILogger logger)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// execute command 
    /// </summary>
    /// <returns></returns>
    public async Task<long[]> ExecuteCommandAsync(IObjectRepositoryContext ctx,  Action<string> onInfoMessage = null)
    {
        var status = new List<long>();
        var cmd = _ctx.Repository.InTransaction() as SqlCommand;
        if (cmd is not null)
        {
            await ExecuteSqlCommandAsync(cmd);
            return status.ToArray();
        }
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            if (onInfoMessage is null)
                await conn.OpenAsync();
            else
            {
                conn.FireInfoMessageEventOnUserErrors = true;
                await conn.OpenAsync();
                conn.InfoMessage += (sender, e) => onInfoMessage(e.Message);
            }
            if (_ctx.UseTransaction)
                await UseTransactionAsync(conn);
            else
                await UseNoTransactionAsync(conn);
            conn.Close();
        }
        return status.ToArray();

        async Task UseTransactionAsync(SqlConnection conn)
        {
            using (var tx = conn.BeginTransaction())
            {
                var cmd = default(SqlCommand);
                try
                {
                    using (cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        await ExecuteSqlCommandAsync(cmd);
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
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
                    await ExecuteSqlCommandAsync(cmd);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
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
                    if (dbParameters is not null && dbParameters.Count() > 0)
                        foreach (var dbParameter in dbParameters)
                            cmd.Parameters.Add(dbParameter);
                    var returnParameter = new SqlParameter("@ReturnVal", SqlDbType.BigInt);
                     returnParameter.Direction = ParameterDirection.ReturnValue;
                    cmd.Parameters.Add(returnParameter);
                    await cmd.ExecuteNonQueryAsync();
                    status.Add(Convert.ToInt64(returnParameter.Value));
                }
            }
            else
            {
                var returnParameter = new SqlParameter("@ReturnVal", SqlDbType.BigInt);
                returnParameter.Direction = ParameterDirection.ReturnValue;
                cmd.Parameters.Add(returnParameter);
                await cmd.ExecuteNonQueryAsync();
                status.Add(Convert.ToInt64(returnParameter.Value));
            }
        }
    }

    public object QueueCommand(string commandText, CommandType commandType, List<object> parameterValues)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("ObjectDataRepository.QueueCommand: command text parameter is empty");
        var dbParameters = GetParameters(parameterValues).FirstOrDefault();
        return new ObjectDataQueuedCommand(commandType, commandText, dbParameters);
    }

    /// <summary>
    /// execute list of queued commands 
    /// </summary>
    /// <returns></returns>
    public async Task ExecuteQueuedCommandsAsync(List<object> queuedCommands, bool useTransaction = false)
    {
        if (queuedCommands.Count == 0)
            throw new InvalidOperationException("SqlServerObjectRepositoryProvider.ExecuteQueuedCommandsAsync: no commands have been queued for execution");
        var commandText = string.Empty;
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {

                        cmd.Transaction = tx;
                        foreach (ObjectDataQueuedCommand queuedCommand in queuedCommands)
                        {
                            if (string.IsNullOrWhiteSpace(queuedCommand.CommandText))
                                throw new ArgumentException("SqlServerObjectRepositoryProvider.ExecuteQueuedCommandsAsync: command text parameter is empty");
                            cmd.CommandType = queuedCommand.CommandType;
                            cmd.CommandText = queuedCommand.CommandText;
                            commandText = cmd.CommandText;
                            cmd.Parameters.Clear();
                            if (queuedCommand.Parameters != null && queuedCommand.Parameters.Length > 0)
                                foreach (var spParameter in queuedCommand.Parameters)
                                    cmd.Parameters.Add(spParameter);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    while (ex.InnerException != null) ex = ex.InnerException;
                    var errorMessage = $"SqlServerObjectRepositoryProvider.ExecuteQueuedCommandAsync: {commandText} {ex.Message}";
                    throw new StorageException(errorMessage, ex);
                }
            }
            conn.Close();
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
                using (var bulkCopy = new SqlBulkCopy(conn as SqlConnection))
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
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetObjectsAsync<TResult>()
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        IReadOnlyList<TResult> resultSet;
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            resultSet = objectReader.ReadAll();
                        }
                    }
                    else
                        throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                }
            }
        }
        return resultSet;
    }

    /// <summary>
    /// execute query that returns a list of immutable objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>()
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: only single parameter value accepted");
        IReadOnlyList<TResult> resultSet;
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            resultSet = objectReader.ReadAllAsImmutable();
                        }
                    }
                    else
                        throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                }
            }
        }
        return resultSet;
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultTypeMapper"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetObjectsAsync<TResult>(Func<IObjectReader<TResult>, TResult> resultTypeMapper = null)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        var resultSet = new List<TResult>();
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                    {
                        if (resultTypeMapper != null)
                            resultSet = await objectReader.ReadAllAsync(resultTypeMapper);
                        else if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                            resultSet = await objectReader.ReadAllAsync();
                        else
                            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                    }
                }
            }
        }
        return resultSet;
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultMapper"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetObjectsAsync<TSource, TResult>(Func<TSource, TResult> resultMapper)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        var resultSet = new List<TResult>();
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            resultSet = await objectReader.ReadAllAsync(resultMapper);
                        }
                    }
                    else
                        throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                }
            }
        }
        return resultSet;
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultMapper"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetObjectsAsync<TSource, TResult>(Func<TSource, TResult> resultMapper, int rowCount)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        var resultSet = new List<TResult>();
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            resultSet = await objectReader.ReadRowsAsync(resultMapper, rowCount);
                        }
                    }
                    else
                        throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                }
            }
        }
        return resultSet;
    }
    /// <summary>
    /// return single object
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    /// <exception cref="StorageException"></exception>
    public async Task<TResult> GetObjectAsync<TResult>()
    {
        var result = default(TResult);
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as SqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using (var dataReader = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
            {
                if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                {
                    using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                    {
                        result = await objectReader.ReadSingleAsync();
                    }
                }
                else
                    throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
            }
            return result;
        }
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            result = await objectReader.ReadSingleAsync();
                        }
                    }
                    else
                        throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
                }
            }
        }
        return result;
    }

    /// <summary>
    /// return single object from type mapper
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultTypeMapper"></param>
    /// <returns></returns>
    /// <exception cref="StorageException"></exception>
    public async Task<TResult> GetObjectAsync<TResult>(Func<IObjectReader<TResult>, TResult> resultTypeMapper)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync<TResult>: only single parameter value accepted");
        var result = default(TResult);
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    using (var objectReader = new SqlServerObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap))
                    {
                        if (resultTypeMapper != null)
                            result = await objectReader.ReadSingleAsync(resultTypeMapper);
                        else if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
                            result = await objectReader.ReadSingleAsync();
                        else
                            throw new StorageException("SqlServerObjectRepositoryProvider.GetObjectAsync<TResult>: object result map parameter is empty");
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// execute query that returns single object by result mapper
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultMapper"></param>
    /// <returns></returns>
    public async Task<TResult> GetObjectAsync<TSource, TResult>(Func<TSource, TResult> resultMapper)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObject: only single parameter value accepted");
        var result = default(TResult);
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            result = await objectReader.ReadSingleAsync(resultMapper);
                        }
                    }
                    else
                        throw new StorageException("SqlServerObjectRepositoryProvider.GetObject: object result map parameter is empty");
                }
            }
        }
        return result;
    }

    /// <summary>
    /// execute query that returns single object by result mapper
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="resultMapper"></param>
    /// <returns></returns>
    public async Task<TResult> GetObjectAsync<TSource, TResult>(Func<IEnumerable<TSource>, TResult> resultMapper)
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObject: only single parameter value accepted");
        var result = default(TResult);
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                _ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
                    {
                        using (var objectReader = new SqlServerObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap))
                        {
                            var resultSet = await objectReader.ReadAllAsync();
                            result = resultMapper(resultSet);
                        }
                    }
                    else
                        throw new StorageException("SqlServerObjectRepositoryProvider.GetObject: object result map parameter is empty");
                }
            }
        }
        return result;
    }

    /// <summary>
    /// return single object by column name
    /// </summary>
    /// <typeparam name="TScalar"></typeparam>
    /// <param name="columnName"></param>
    /// <returns></returns>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, string columnName) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteScalar: only single parameter value accepted");
        var scalarResult = default(TScalar);
        using (var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    using (var objectReader = new SqlServerObjectDataReader<TScalar>(dataReader))
                    {
                        scalarResult = objectReader.ReadScalar<TScalar>(columnName);
                    }
                }
            }
            conn.Close();
        }
        return scalarResult;
    }

    /// <summary>
    /// return single scalar object at first column in resultset
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteScalar: only single parameter value accepted");
        var scalarResult = default(TScalar);
        using (var conn = ctx.Repository.CreateConnection().As<SqlConnection>(ctx.Repository.ConnectionString))
        {
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                ctx.SetCommand(cmd);
                SetParameters(cmd);
                using (var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    using (var objectReader = new SqlServerObjectDataReader<TScalar>(dataReader))
                    {
                        scalarResult = objectReader.ReadScalar<TScalar>();
                    }
                }
            }
            conn.Close();
        }
        return scalarResult;
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
                var dbParameters = new List<IDbDataParameter>();
                foreach (var propInfo in paramProps)
                {
                    var propValue = propInfo.GetValue(paramValue);
                    switch (propInfo.Name)
                    {
                        case "tableName":
                            bulkInsertParameters.tableName = Convert.ToString(propValue);
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
    DbType GetDbTypeFromParameterValue(object value)
    {
        var valueType = value.GetType();
        return GetDbTypeFromParameterValue(valueType);
    }

    /// <summary>
    /// return DbType from type of parameter value
    /// </summary>
    /// <param name="valueType">parameter value type</param>
    /// <returns></returns>
    DbType GetDbTypeFromParameterValue(Type parameterValueType)
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
            foreach (var p in GetParameters())
            {
                foreach (var e in p)
                    cmd.Parameters.Add(e);
                break;
            }
        }
    }

    public void Dispose()
    {
    }

    public Task<ICollection<TResult>> GetObjectsAsync<TResult>(Func<IDataReader, TResult> dataReaderMapper)
    {
        throw new NotImplementedException();
    }

    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        var resultSet = new List<TResult>();
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var mapReader = new ObjectDataMapReader<TResult>(dataReader);
        while (dataReader.Read())
            resultSet.Add(dataReaderMapper(mapReader));
        return resultSet;
    }

    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        var result = default(TResult);
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (dataReader.Read())
        {
            var mapReader = new ObjectDataMapReader<TResult>(dataReader);
            result = dataReaderMapper(mapReader);
        }
        return result;
    }

    public async Task<TResult> GetObjectFromSourceAsync<TSource, TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TSource>, TResult> dataReaderMapper)
    {
        var result = default(TResult);
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (dataReader.Read())
        {
            var mapReader = new ObjectDataMapReader<TSource>(dataReader);
            result = dataReaderMapper(mapReader);
        }
        return result;
    }

    public Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataMapper<TResult>, TResult> dataMapper) where TResult : class
    {
        throw new NotImplementedException();
    }

    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var mapReader = new ObjectDataMapReader<TResult>(dataReader);
        reducer?.Invoke(MapReduce());

        IEnumerable<TResult> MapReduce()
        {
            while (dataReader.Read())
                yield return mapper(mapReader);
        }
    }

    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> mapper, Action<IEnumerable<TResult>> reducer)
        => throw new NotImplementedException($"{this.GetType().Name}.ExecuteMapReduceAsync: not implemented");

    public Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<string, int, TResult> mapper)
    {
        throw new NotImplementedException();
    }

    public Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> mapper)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<string, int, TResult> dataMapper)
    {
        throw new NotImplementedException();
    }

    public Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> dataMapper)
    {
        throw new NotImplementedException();
    }

    public Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<string, int, TScalar> dataMapper) where TScalar : struct
    {
        throw new NotImplementedException();
    }

    public Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<object[], TScalar> dataMapper) where TScalar : struct
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Executes a query asynchronously and maps the results to a collection using an <see cref="IObjectDataRecord"/>
    /// mapper, eliminating intermediate <c>object[]</c> allocation and value-type boxing.
    /// </summary>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> dataMapper)
    {
        if (dataMapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
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
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetImmutableObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
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
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetObjectAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
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

    /// <summary>
    /// Executes a scalar query asynchronously and maps the result using an <see cref="IObjectDataRecord"/> mapper.
    /// </summary>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TScalar> dataMapper) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.GetScalarAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
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

    /// <summary>
    /// Executes a map-reduce operation using an <see cref="IObjectDataRecord"/> mapper with yield-return streaming.
    /// </summary>
    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataRecord, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        if (mapper is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: mapper parameter is null");
        if (reducer is null)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: reducer parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"SqlServerObjectRepositoryProvider.ExecuteMapReduceAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<SqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var record = new AdoNetDataRecord().SetReader(dataReader);
        reducer.Invoke(MapReduce());

        IEnumerable<TResult> MapReduce()
        {
            while (dataReader.Read())
                yield return mapper(record);
        }
    }

}
