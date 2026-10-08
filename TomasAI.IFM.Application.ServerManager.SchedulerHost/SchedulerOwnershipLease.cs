using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

/// <summary>Holds the PostgreSQL session lock protecting one unclustered Quartz scheduler.</summary>
public sealed class SchedulerOwnershipLease(
    NpgsqlDataSource dataSource, SchedulerHostOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private NpgsqlConnection? connection;
    private volatile bool owned;

    /// <summary>Gets whether the dedicated lock session was last confirmed healthy.</summary>
    public bool IsOwned => owned;

    /// <summary>Exclusively holds a checked-out authenticated session for the host's entire lifetime.</summary>
    public async Task AcquireAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (owned) return;
            // DataSource.ConnectionString redacts credentials. Open through the configured
            // source so password/token authentication is retained; never return this session
            // to its pool until the lease has been explicitly released.
            connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (var identity = connection.CreateCommand())
            {
                identity.CommandTimeout = 5;
                identity.CommandText = "SELECT set_config('application_name', 'IFM Scheduler Ownership', false);";
                await identity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = "SELECT pg_try_advisory_lock($1);";
            // Quartz's name identifies its rows within this database, even across environment labels.
            command.Parameters.AddWithValue(LockKey(options.SchedulerName));
            owned = (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            if (!owned)
                throw new InvalidOperationException("SchedulerHost.OWNERSHIP.CONFLICT; another host owns this Quartz scheduler.");
        }
        catch
        {
            owned = false;
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Checks the actual lock session before starting work; loss fences new launches.</summary>
    public async Task EnsureOwnedAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!owned || connection is null)
                throw new InvalidOperationException("SchedulerHost.OWNERSHIP.LOST; runtime ownership is unavailable.");
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            command.CommandTimeout = 5;
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            owned = false;
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Derives a stable database-local lock identity from Quartz's persisted scheduler name.</summary>
    public static long LockKey(string schedulerName)
        => BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(
            Encoding.UTF8.GetBytes("IFM.SchedulerHost:" + schedulerName)));

    /// <summary>Releases the advisory lock before returning the authenticated session to its pool.</summary>
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                if (owned && connection is not null)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandTimeout = 5;
                    command.CommandText = "SELECT pg_advisory_unlock($1);";
                    command.Parameters.AddWithValue(LockKey(options.SchedulerName));
                    await command.ExecuteScalarAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                owned = false;
                if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
                connection = null;
            }
        }
        finally { gate.Release(); }
    }
}
