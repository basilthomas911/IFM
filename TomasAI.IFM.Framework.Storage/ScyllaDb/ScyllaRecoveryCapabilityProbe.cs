using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>Checks the configured Scylla keyspace and its dedicated TTL-backed recovery probe table.</summary>
public sealed class ScyllaRecoveryCapabilityProbe(string connectionString) : IAsyncDisposable
{
    readonly ScyllaDbConnection connection = new(connectionString);

    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        var session = await connection.CreateSessionAsync().WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(session.Keyspace, connection.DefaultKeyspace, StringComparison.OrdinalIgnoreCase))
            return false;
        var identity = await session.ExecuteAsync(new SimpleStatement(
            "SELECT table_name FROM system_schema.tables WHERE keyspace_name=? AND table_name=?",
            connection.DefaultKeyspace, "ifm_recovery_probe")).WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!identity.Any()) return false;
        var id = Guid.NewGuid();
        try
        {
            await session.ExecuteAsync(new SimpleStatement(
                "INSERT INTO ifm_recovery_probe (probe_id, observed_at_utc) VALUES (?, ?) USING TTL 30",
                id, DateTime.UtcNow)).WaitAsync(cancellationToken).ConfigureAwait(false);
            var read = await session.ExecuteAsync(new SimpleStatement(
                "SELECT probe_id FROM ifm_recovery_probe WHERE probe_id=?", id))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return read.FirstOrDefault()?.GetValue<Guid>("probe_id") == id;
        }
        finally
        {
            try
            {
                await session.ExecuteAsync(new SimpleStatement(
                    "DELETE FROM ifm_recovery_probe WHERE probe_id=?", id))
                    .WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
            catch (Exception) { /* TTL bounds any residue. */ }
        }
    }

    public async ValueTask DisposeAsync() => await connection.ShutdownAsync().ConfigureAwait(false);
}
