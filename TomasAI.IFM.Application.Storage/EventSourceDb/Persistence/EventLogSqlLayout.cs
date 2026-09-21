using System.Text.RegularExpressions;
using Npgsql;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

/// <summary>Immutable, allowlisted event-log SQL routing. Benchmark-only behavior has a separate guarded factory.</summary>
internal sealed class EventLogSqlLayout
{
    internal static EventLogSqlLayout Current { get; } = new(false);
    readonly bool _v2;
    readonly bool _benchmark;
    internal bool BatchProjectionMarkers { get; }
    EventLogSqlLayout(bool v2, bool batchProjectionMarkers = false, bool benchmark = false)
        => (_v2, BatchProjectionMarkers, _benchmark) = (v2, batchProjectionMarkers, benchmark);

    internal static EventLogSqlLayout ForProduction(EventLogPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options.TableTarget switch
        {
            EventLogTableTarget.Legacy => Current,
            EventLogTableTarget.EventLogV2 => new EventLogSqlLayout(true),
            _ => throw new ArgumentOutOfRangeException(nameof(options.TableTarget))
        };
    }

    internal static EventLogSqlLayout ForBenchmark(string connectionString, bool v2, bool batchProjectionMarkers = false)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            !Regex.IsMatch(connection.Database ?? "", @"\Aifm_eventlog_bench_[a-f0-9]{12}_[a-z0-9_]+\z"))
            throw new ArgumentException("Benchmark layouts require a loopback-only, uniquely named benchmark database.");
        return new EventLogSqlLayout(v2, batchProjectionMarkers, benchmark: true);
    }

    internal void ValidateConnection(string connectionString)
    {
        // A benchmark layout must never be reusable against an application database.
        if (_benchmark) _ = ForBenchmark(connectionString, _v2, BatchProjectionMarkers);
    }

    internal string Resolve(string sql) => _v2
        ? Regex.Replace(sql, @"\bevent_log\b", "event_log_v2", RegexOptions.CultureInvariant)
        : sql;
}
