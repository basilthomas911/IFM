using System.Text.RegularExpressions;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

/// <summary>Immutable, allowlisted event-log SQL routing. Benchmark-only behavior has a separate guarded factory.</summary>
internal sealed class EventLogSqlLayout
{
    internal static EventLogSqlLayout Current { get; } = new();
    readonly bool _benchmark;
    internal bool BatchProjectionMarkers { get; }
    EventLogSqlLayout(bool batchProjectionMarkers = false, bool benchmark = false)
        => (BatchProjectionMarkers, _benchmark) = (batchProjectionMarkers, benchmark);

    internal static EventLogSqlLayout ForProduction(EventLogPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return Current;
    }

    internal static EventLogSqlLayout ForBenchmark(string connectionString, bool batchProjectionMarkers = false)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            !Regex.IsMatch(connection.Database ?? "", @"\Aifm_eventlog_bench_[a-f0-9]{12}_[a-z0-9_]+\z"))
            throw new ArgumentException("Benchmark layouts require a loopback-only, uniquely named benchmark database.");
        return new EventLogSqlLayout(batchProjectionMarkers, benchmark: true);
    }

    internal void ValidateConnection(string connectionString)
    {
        // A benchmark layout must never be reusable against an application database.
        if (_benchmark) _ = ForBenchmark(connectionString, BatchProjectionMarkers);
    }

    internal string Resolve(string sql) => sql;
}
