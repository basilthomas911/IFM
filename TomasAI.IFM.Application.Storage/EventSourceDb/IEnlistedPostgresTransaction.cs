using Npgsql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Request-scoped operations on an EventSource-owned PostgreSQL transaction.</summary>
public interface IEnlistedPostgresTransaction
{
    Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken cancellationToken);
    Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken);
}
