using Cassandra;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Text;
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
        {
            await ExecuteSqlCommandAsync(cmd as NpgsqlCommand);
            return [-1L];
        }
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        if (_ctx.UseTransaction)
            await UseTransactionAsync(conn);
        else
            await UseNoTransactionAsync(conn);
        conn.Close();
        return [-1L];

        async Task UseTransactionAsync(NpgsqlConnection conn)
        {
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            try
            {
                cmd.Transaction = tx;
                await ExecuteSqlCommandAsync(cmd);
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                var errorMessage = $"{ProviderTypeName}.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }

        async Task UseNoTransactionAsync(NpgsqlConnection conn)
        {
            using var cmd = conn.CreateCommand();
            try
            {
                    await ExecuteSqlCommandAsync(cmd);
            }
            catch (Exception ex)
            {
                var errorMessage = $"{ProviderTypeName}.ExecuteCommandAsync: {cmd.CommandText} {ex.Message}";
                throw new StorageException(errorMessage, ex);
            }
        }

        async Task ExecuteSqlCommandAsync(NpgsqlCommand cmd)
        {
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
                        await cmd.ExecuteNonQueryAsync();
                    }
                    else
                        await cmd.ExecuteNonQueryAsync();
                }
            }
            else if (cmd.CommandType == CommandType.StoredProcedure)
            {
                var returnParameter = cmd.Parameters.AddWithValue(NpgsqlDbType.Integer, default);
                returnParameter.Direction = ParameterDirection.Output;
                await cmd.ExecuteNonQueryAsync();
            }
            else
                await cmd.ExecuteNonQueryAsync();
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
                        cmd.Parameters.Add(spParameter);
                            await cmd.ExecuteNonQueryAsync();
             }
             tx.Commit();
            conn.Close();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            conn.Close();
            while (ex.InnerException != null) ex = ex.InnerException;
               var errorMessage = $"{ProviderTypeName}.ExecuteQueuedCommandAsync: {commandText} {ex.Message}";
            throw new StorageException(errorMessage, ex);
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
    public async Task<IReadOnlyList<TResult>> GetObjectsAsync<TResult>()
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        IReadOnlyList<TResult> resultSet;
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
        {
                using var objectReader = new PostgresObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap);
                resultSet = objectReader.ReadAll();
        }
        else
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
         return resultSet;
    }

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>,TResult> dataReaderMapper)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
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

    /// <summary>
    /// execute query that returns a list of objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<string, int, TResult> dataMapper)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var resultSet = GetResultSet(dataReader, dataMapper);
        return resultSet;
    }

    public async Task<ICollection<TResult>> GetObjectsAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> dataMapper)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var resultSet = GetResultSet(dataReader, dataMapper);
        return resultSet;
    }

    /// <summary>
    /// Executes a map-reduce operation on a data source using the specified mapper and reducer functions.
    /// </summary>
    /// <remarks>The method opens a connection to the data source, executes a command, and applies the
    /// <paramref name="mapper"/> function to each record retrieved. The results are then passed to the <paramref
    /// name="reducer"/> action for further processing.</remarks>
    /// <typeparam name="TResult">The type of the result produced by the mapper function.</typeparam>
    /// <param name="mapper">A function that maps each record from the data source to a result of type <typeparamref name="TResult"/>.</param>
    /// <param name="reducer">An action that processes the collection of mapped results.</param>
    /// <returns></returns>
    /// <exception cref="StorageException">Thrown if more than one parameter value is provided to the operation.</exception>
    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> mapper, Action<IEnumerable<TResult>> reducer)
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var mapReader = new ObjectDataMapReader<TResult>(dataReader);
        reducer?.Invoke(mapReducer());

        IEnumerable<TResult> mapReducer()
        {
            while (dataReader.Read())
            {
                yield return mapper(mapReader);
            }
        }
    }

    public async ValueTask ExecuteMapReduceAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> mapper, Action<IEnumerable<TResult>> reducer)
        => throw new NotImplementedException($"{ProviderTypeName}.ExecuteMapReduceAsync: not implemented");

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
    /// execute query that returns a list of immutable objects
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public async Task<IReadOnlyList<TResult>> GetImmutableObjectsAsync<TResult>()
    {
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetImmutableObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
        IReadOnlyList<TResult> resultSet;
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
        {
            using var objectReader = new PostgresObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap);
            resultSet = objectReader.ReadAllAsImmutable();
        }
        else
             throw new StorageException($"{ProviderTypeName}.GetImmutableObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
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
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        using var objectReader = new PostgresObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap);
        IReadOnlyList < TResult > resultSet;
        if (resultTypeMapper != null)
            resultSet = await objectReader.ReadAllAsync(resultTypeMapper);
        else if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
            resultSet = await objectReader.ReadAllAsync();
        else
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
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
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        IReadOnlyList<TResult> resultSet;
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
        {
             using var objectReader = new PostgresObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap);
             resultSet = await objectReader.ReadAllAsync(resultMapper);
        }
        else
             throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
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
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: only single parameter value accepted");
        var resultSet = new List<TResult>();
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
        {
            using var objectReader = new PostgresObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap);
            resultSet = await objectReader.ReadRowsAsync(resultMapper, rowCount);
        }
        else
            throw new StorageException($"{ProviderTypeName}.GetObjectsAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
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
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
            {
                using var objectReader = new PostgresObjectDataReader<TResult>(dataReader1, _ctx.Repository.ResultTypeMap);
                result = await objectReader.ReadSingleAsync();
            }
            else
                throw new StorageException($"{ProviderTypeName}.GetObjectAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
        }
        else
        {
            using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
            {
                using var objectReader = new PostgresObjectDataReader<TResult>(dataReader2, _ctx.Repository.ResultTypeMap);
                result = await objectReader.ReadSingleAsync();
            }
            else
                throw new StorageException($"{ProviderTypeName}.GetObjectAsync: unable to map resultset to type: '{typeof(TResult).Name}'");
        }
       return result;
    }

    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TResult>, TResult> dataReaderMapper)
    {
        var result = default(TResult);
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (dataReader1.Read())
            {
                var mapReader = new ObjectDataMapReader<TResult>(dataReader1);
                result = dataReaderMapper(mapReader);
            }
        }
        else
        {
            using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (dataReader2.Read())
            {
                var mapReader = new ObjectDataMapReader<TResult>(dataReader2);
                result = dataReaderMapper(mapReader);
            }
        }
        return result;
    }

    /// <summary>
    /// Asynchronously retrieves a single object from the repository using the specified data mapper function.
    /// </summary>
    /// <remarks>This method executes a query against the repository and maps the result to an object of type
    /// <typeparamref name="TResult"/>  using the provided <paramref name="dataMapper"/> function. The method supports
    /// both transactional and non-transactional contexts.</remarks>
    /// <typeparam name="TResult">The type of the object to be retrieved.</typeparam>
    /// <param name="ctx">The repository context used to execute the query. Must not be <see langword="null"/>.</param>
    /// <param name="dataMapper">A function that maps the data from the repository to an object of type <typeparamref name="TResult"/>. The
    /// function takes a string and an integer as input parameters and returns an object of type <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the object of type <typeparamref
    /// name="TResult"/>  if found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="StorageException">Thrown if <paramref name="dataMapper"/> is <see langword="null"/> or if the repository context contains more
    /// than one parameter value.</exception>
    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<string, int, TResult> dataMapper) 
    {
        var result = default(TResult);
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: dataMapper parameter is null");
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            result = GetSingle(dataReader1, dataMapper);
        }
        else
        {
            using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            result = GetSingle(dataReader2, dataMapper);
        }
        return result;
    }

    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<object[], TResult> dataMapper)
    {
        var result = default(TResult);
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: dataMapper parameter is null");
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            result = GetSingle(dataReader1, dataMapper);
        }
        else
        {
            using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            result = GetSingle(dataReader2, dataMapper);
        }
        return result;
    }

    public async Task<TResult?> GetObjectAsync<TResult>(IObjectRepositoryContext ctx, Func<IObjectDataMapper<TResult>, TResult> dataMapper) where TResult : class
    {
        var result = default(TResult);
        if (_ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = _ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            _ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (dataReader1.Read())
            {
                var mapReader = new ObjectDataMapReader<TResult>(dataReader1);
                //result = dataReaderMapper(mapReader);
                result = default!;
            }
        }
        else
        {
            using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            _ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (dataReader2.Read())
            {
                var mapReader = new ObjectDataMapReader<TResult>(dataReader2);
                //result = dataReaderMapper(mapReader);
                result = default!;
            }
        }
        return result;
    }


    public async Task<TResult> GetObjectFromSourceAsync<TSource, TResult>(IObjectRepositoryContext ctx, Func<IObjectMapReader<TSource>, TResult> dataReaderMapper)
    {
        var result = default(TResult);
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync: only single parameter value accepted");
        var txCmd = ctx.Repository.InTransaction() as NpgsqlCommand;
        if (txCmd is not null)
        {
            ctx.SetCommand(txCmd);
            SetParameters(txCmd);
            using var dataReader1 = await txCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            if (dataReader1.Read())
            {
                var mapReader = new ObjectDataMapReader<TSource>(dataReader1);
                result = dataReaderMapper(mapReader);
            }
        }
        else
        {
            using var conn = ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            ctx.SetCommand(cmd);
            SetParameters(cmd);
            using var dataReader2 = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            if (dataReader2.Read())
            {
                var mapReader = new ObjectDataMapReader<TSource>(dataReader2);
                result = dataReaderMapper(mapReader);
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
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync<TResult>: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        using var objectReader = new PostgresObjectDataReader<TResult>(dataReader, _ctx.Repository.ResultTypeMap);
        var result = default(TResult);
        if (resultTypeMapper != null)
            result = await objectReader.ReadSingleAsync(resultTypeMapper);
        else if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TResult)))
            result = await objectReader.ReadSingleAsync();
        else
            throw new StorageException($"{ProviderTypeName}.GetObjectAsync<TResult>: object result map parameter is empty");
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
            throw new StorageException($"{ProviderTypeName}.GetObject: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        var result = default(TResult);
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
        {
            using var objectReader = new PostgresObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap);
            result = await objectReader.ReadSingleAsync(resultMapper);
        }
        else
             throw new StorageException($"{ProviderTypeName}.GetObject: object result map parameter is empty");
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
            throw new StorageException($"{ProviderTypeName}.GetObject: only single parameter value accepted");
        var result = default(TResult);
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(_ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        _ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        if (_ctx.Repository.ResultTypeMap.ContainsKey(typeof(TSource)))
        {
            using var objectReader = new PostgresObjectDataReader<TSource>(dataReader, _ctx.Repository.ResultTypeMap);
            var resultSet = await objectReader.ReadAllAsync();
            result = resultMapper(resultSet);
        }
        else
               throw new StorageException($"{ProviderTypeName}.GetObject: object result map parameter is empty");
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
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: only single parameter value accepted");
        using var conn = ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        using var objectReader = new PostgresObjectDataReader<TScalar>(dataReader);
        var scalarResult = objectReader.ReadScalar<TScalar>(columnName);
        return scalarResult;
    }

    /// <summary>
    /// return single scalar object at first column in resultset
    /// </summary>
    /// <typeparam name="TScalar"></typeparam>
    /// <returns></returns>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx) where TScalar : struct
    {
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: only single parameter value accepted");
        var scalarResult = default(TScalar);
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        using (var objectReader = new PostgresObjectDataReader<TScalar>(dataReader))
        scalarResult = objectReader.ReadScalar<TScalar>();
        return scalarResult;
    }

    /// <summary>
    /// return single scalar object at first column in resultset
    /// </summary>
    /// <typeparam name="TScalar"></typeparam>
    /// <returns></returns>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<string,int, TScalar> dataMapper) where TScalar : struct
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        return GetScalar(dataReader, dataMapper);
    }

    /// <summary>
    /// Executes a scalar query asynchronously and maps the result to a value of the specified type.
    /// </summary>
    /// <remarks>This method opens a database connection, executes the query defined in the repository
    /// context, and maps the first result to the specified scalar type using the provided <paramref name="dataMapper"/>
    /// function. The connection is automatically closed after the operation completes.</remarks>
    /// <typeparam name="TScalar">The type of the scalar value to return. Must be a value type.</typeparam>
    /// <param name="ctx">The repository context containing query parameters and configuration. Must not be null.</param>
    /// <param name="dataMapper">A function that maps the query result to the specified scalar type. Must not be null.</param>
    /// <returns>The scalar value of type <typeparamref name="TScalar"/> returned by the query.</returns>
    /// <exception cref="StorageException">Thrown if <paramref name="dataMapper"/> is null, if the repository context contains more than one parameter
    /// value, or if an error occurs during query execution.</exception>
    public async Task<TScalar> GetScalarAsync<TScalar>(IObjectRepositoryContext ctx, Func<object[], TScalar> dataMapper) where TScalar : struct
    {
        if (dataMapper is null)
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: dataMapper parameter is null");
        if (ctx.ParameterValues.Count > 1)
            throw new StorageException($"{ProviderTypeName}.ExecuteScalar: only single parameter value accepted");
        using var conn = _ctx.Repository.CreateConnection().As<NpgsqlConnection>(ctx.Repository.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        ctx.SetCommand(cmd);
        SetParameters(cmd);
        using var dataReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
        return GetScalar(dataReader, dataMapper);
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
    static List<TResult> GetResultSet<TResult>(NpgsqlDataReader dataReader, Func<string, int, TResult> dataMapper)
    {
        List<TResult> resultSet = [];
        var pipe = "|";
        var row = new object[dataReader.FieldCount];
        var sb = new StringBuilder();
        while (dataReader.Read())
        {
            var index = 0;
            dataReader.GetValues(row);
            sb.Clear();
            foreach (var value in row)
            {
                if (index > 0)
                    sb.Append(pipe);
                sb.Append(value?.ToString() ?? string.Empty);
                index++;
            }
            var start = 0;
            resultSet.Add(dataMapper(sb.ToString(), start));
        }
        return resultSet;
    }

    /// <summary>
    /// Reads all rows from the specified <see cref="NpgsqlDataReader"/> and maps them to a strongly-typed result set.
    /// </summary>
    /// <remarks>The method reads all rows from the <paramref name="dataReader"/> sequentially and applies the
    /// <paramref name="dataMapper"/>  function to each row to produce the result set. The caller is responsible for
    /// ensuring that the <paramref name="dataReader"/>  is properly initialized and positioned before the first
    /// row.</remarks>
    /// <typeparam name="TResult">The type of the objects in the result set.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. Must be positioned before the first row.</param>
    /// <param name="dataMapper">A delegate that maps an array of column values to an instance of <typeparamref name="TResult"/>.  The array
    /// contains the values of the current row, with each element corresponding to a column in the result set.</param>
    /// <returns>A <see cref="LinkedList{T}"/> containing the mapped results of type <typeparamref name="TResult"/>.  The list
    /// will be empty if the <paramref name="dataReader"/> contains no rows.</returns>
    static List<TResult> GetResultSet<TResult>(NpgsqlDataReader dataReader, Func<object[], TResult> dataMapper)
    {
        List<TResult> resultSet = [];
        var row = new object[dataReader.FieldCount];
        while (dataReader.Read())
        {
            dataReader.GetValues(row);
            resultSet.Add(dataMapper(row));
        }
        return resultSet;
    }


    /// <summary>
    /// Reads a single row from the provided <see cref="NpgsqlDataReader"/> and maps it to a result of type
    /// <typeparamref name="TResult"/>.
    /// </summary>
    /// <remarks>This method reads only the first row from the <paramref name="dataReader"/> and uses the
    /// <paramref name="dataMapper"/> function to transform the row data into a result. The row data is concatenated
    /// into a single string with fields separated by a pipe ("|") character.</remarks>
    /// <typeparam name="TResult">The type of the result produced by the <paramref name="dataMapper"/> function.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. Must contain at least one row.</param>
    /// <param name="dataMapper">A function that maps the concatenated row data and a starting index to a result of type <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>An instance of <typeparamref name="TResult"/> representing the mapped result of the first row in the <paramref
    /// name="dataReader"/>. If no rows are available, the default value of <typeparamref name="TResult"/> is returned.</returns>
    static TResult GetSingle<TResult>(NpgsqlDataReader dataReader, Func<string, int, TResult> dataMapper)
    {
        TResult result = default!;
        var pipe = "|";
        var row = new object[dataReader.FieldCount];
        var sb = new StringBuilder();
        if (dataReader.Read())
        {
            var index = 0;
            dataReader.GetValues(row);
            sb.Clear();
            foreach (var value in row)
            {
                if (index > 0)
                    sb.Append(pipe);
                sb.Append(value?.ToString() ?? string.Empty);
                index++;
            }
            var start = 0;
            result = dataMapper(sb.ToString(), start);
        }
        return result;
    }

    /// <summary>
    /// Reads a single row from the provided <see cref="NpgsqlDataReader"/> and maps it to a result of type
    /// <typeparamref name="TResult"/>.
    /// </summary>
    /// <remarks>This method reads only the first row from the <paramref name="dataReader"/>. If the reader
    /// contains no rows, the method returns the default value of <typeparamref name="TResult"/>. The caller is
    /// responsible for ensuring that the <paramref name="dataReader"/> is properly initialized and disposed.</remarks>
    /// <typeparam name="TResult">The type of the result produced by the <paramref name="dataMapper"/> function.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. Must be positioned before the first row.</param>
    /// <param name="dataMapper">A function that maps an array of objects representing the row's values to an instance of <typeparamref
    /// name="TResult"/>.</param>
    /// <returns>An instance of <typeparamref name="TResult"/> representing the mapped row, or the default value of <typeparamref
    /// name="TResult"/> if no rows are available.</returns>
    static TResult GetSingle<TResult>(NpgsqlDataReader dataReader, Func<object[], TResult> dataMapper)
    {
        TResult result = default!;
        if (dataReader.Read())
        {
            var row = new object[dataReader.FieldCount];
            dataReader.GetValues(row);
            result = dataMapper(row);
        }
        return result;
    }

    /// <summary>
    /// Retrieves a scalar value from the current row of the provided <see cref="NpgsqlDataReader"/>  using a custom
    /// mapping function.
    /// </summary>
    /// <remarks>This method reads the first row of the <paramref name="dataReader"/> and applies the 
    /// <paramref name="dataMapper"/> function to extract a scalar value. If the data reader contains  no rows, the
    /// method returns the default value of <typeparamref name="TScalar"/>.</remarks>
    /// <typeparam name="TScalar">The type of the scalar value to retrieve. Must be a value type.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. Must not be null.</param>
    /// <param name="dataMapper">A function that maps a string representation of the data and a starting index to a value of type <typeparamref
    /// name="TScalar"/>.</param>
    /// <returns>The scalar value of type <typeparamref name="TScalar"/> retrieved from the current row of the data reader. If no
    /// rows are available, returns the default value of <typeparamref name="TScalar"/>.</returns>
    static TScalar GetScalar<TScalar>(NpgsqlDataReader dataReader, Func<string, int, TScalar> dataMapper) where TScalar : struct
    {
        TScalar result = default!;
        var row = new object[dataReader.FieldCount];
        var sb = new StringBuilder();
        if (dataReader.Read())
        {
            dataReader.GetValues(row);
            sb.Clear();
            foreach (var value in row)
            {
                sb.Append(value?.ToString() ?? string.Empty);
                break;
            }
            var start = 0;
            result = dataMapper(sb.ToString(), start);
        }
        return result;
    }

    /// <summary>
    /// Reads a single value from the provided <see cref="NpgsqlDataReader"/> and maps it to a scalar value of type
    /// <typeparamref name="TScalar"/>.
    /// </summary>
    /// <remarks>This method reads only the first row of the <see cref="NpgsqlDataReader"/> and expects the
    /// row to contain a single value. The <paramref name="dataMapper"/> function is responsible for converting the
    /// row's value to the desired scalar type.</remarks>
    /// <typeparam name="TScalar">The type of the scalar value to return. Must be a value type.</typeparam>
    /// <param name="dataReader">The <see cref="NpgsqlDataReader"/> to read data from. The reader must be positioned before the first row.</param>
    /// <param name="dataMapper">A function that maps the retrieved row data to a scalar value of type <typeparamref name="TScalar"/>.  The
    /// function receives an array containing the row's values.</param>
    /// <returns>A scalar value of type <typeparamref name="TScalar"/> if a row is available; otherwise, the default value of
    /// <typeparamref name="TScalar"/>.</returns>
    static TScalar GetScalar<TScalar>(NpgsqlDataReader dataReader, Func<object[], TScalar> dataMapper) where TScalar : struct
    {
        TScalar result = default!;
        if (dataReader.Read())
        {
            var row = new object[1];
            dataReader.GetValues(row);
            result = dataMapper(row);
        }
        return result;
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
