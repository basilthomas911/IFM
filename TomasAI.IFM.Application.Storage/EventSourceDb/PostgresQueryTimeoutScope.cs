using TomasAI.IFM.Framework.Storage.Postgres;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Applies query-owned server timers on a caller-owned transaction without leaking settings to the pool.</summary>
internal sealed class PostgresQueryTimeoutScope(NpgsqlConnection connection, NpgsqlTransaction transaction,
    PostgresQueryTimeout? defaults = null)
{
    readonly PostgresQueryTimeout defaultTimeout = (defaults ?? PostgresQueryTimeout.Default).Validate();
    PostgresQueryTimeout? activeTimeout;

    internal async Task ApplyAsync(PostgresQueryTimeout? timeout, CancellationToken token)
    {
        var selected = (timeout ?? defaultTimeout).Validate();
        if (selected == activeTimeout) return;
        // The query owns the server timer. A second provider timer must not expire earlier.
        await using var configure = new NpgsqlCommand(
            EventSourceDbSql.PostgresQueryTimeoutScope_ApplyAsync_1, connection, transaction)
            { CommandTimeout = 0 };
        PostgresValueBinding.AddParameter(configure, checked((int)Math.Ceiling(selected.StatementTimeout.TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture));
        PostgresValueBinding.AddParameter(configure, checked((int)Math.Ceiling((selected.LockTimeout ?? selected.StatementTimeout).TotalMilliseconds)).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await configure.ExecuteNonQueryAsync(token)
            .ConfigureAwait(false);
        activeTimeout = selected;
    }
}
