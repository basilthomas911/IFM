using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Executes bound statements on a caller-owned configuration connection and transaction.</summary>
internal sealed class ConfigurationDbSession(NpgsqlConnection connection,NpgsqlTransaction transaction):IEnlistedPostgresTransaction
{
    readonly PostgresQueryTimeoutScope timeouts = new(connection, transaction);
    NpgsqlCommand Command(string sql,object?[] values)
    {
        var command=new NpgsqlCommand(sql,connection,transaction) { CommandTimeout = 0 };
        foreach(var value in values) command.Parameters.Add(value is NpgsqlParameter parameter?parameter:new NpgsqlParameter{Value=value??DBNull.Value});
        return command;
    }
    public async Task<int> ExecuteAsync(string sql,object?[] parameters,CancellationToken token, PostgresQueryTimeout? timeout = null)
    {await timeouts.ApplyAsync(timeout,token).ConfigureAwait(false);await using var command=Command(sql,parameters);return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);}
    public async Task<object?> ScalarAsync(string sql,object?[] parameters,CancellationToken token, PostgresQueryTimeout? timeout = null)
    {await timeouts.ApplyAsync(timeout,token).ConfigureAwait(false);await using var command=Command(sql,parameters);var value=await command.ExecuteScalarAsync(token).ConfigureAwait(false);return value is DBNull?null:value;}
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql,object?[] parameters,Func<NpgsqlDataReader,T> map,CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout,token).ConfigureAwait(false);
        await using var command=Command(sql,parameters);await using var reader=await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var values=new List<T>();while(await reader.ReadAsync(token).ConfigureAwait(false))values.Add(map(reader));return values;
    }
}
