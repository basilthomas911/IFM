using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Cassandra;
using Perfolizer.Mathematics.OutlierDetection;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Compares the legacy filtered ITI trend/mode lookup with its query-shaped projection.
/// Benchmark-owned tables isolate the measurement from application data while preserving
/// the production canonical primary-key order and proposed projection access pattern.
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(5)]
[IterationCount(100)]
[InvocationCount(1)]
[Outliers(OutlierMode.DontRemove)]
public class ScyllaItiQueryProjectionBenchmarks
{
    const string ConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string CredentialVariable = "SCYLLADB_TEST_KEY";
    const string ContractId = "__ifm_swo05_benchmark__";
    const string TargetTrend = "UpTrend";
    const string TargetMode = "TrendDirectionChanged";
    const int DayCount = 120;

    const string CreateCanonicalTable = """
        CREATE TABLE IF NOT EXISTS swo05_iti_query_canonical_benchmark (
            contractId text,
            valueDate date,
            timePeriod text,
            intrinsicTimeMode text,
            intrinsicTimeTrend text,
            intrinsicTimeGroupId int,
            sequenceId bigint,
            PRIMARY KEY (
                contractId,
                valueDate,
                timePeriod,
                intrinsicTimeMode,
                intrinsicTimeTrend,
                intrinsicTimeGroupId,
                sequenceId))
        WITH CLUSTERING ORDER BY (
            valueDate DESC,
            timePeriod DESC,
            intrinsicTimeMode DESC,
            intrinsicTimeTrend DESC,
            intrinsicTimeGroupId DESC,
            sequenceId DESC);
        """;

    const string CreateProjectionTable = """
        CREATE TABLE IF NOT EXISTS swo05_iti_query_projection_v2_benchmark (
            contractId text,
            intrinsicTimeTrend text,
            intrinsicTimeMode text,
            yearMonth int,
            valueDate date,
            sequenceId bigint,
            timePeriod text,
            intrinsicTimeGroupId int,
            PRIMARY KEY (
                (contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth),
                valueDate,
                sequenceId,
                timePeriod,
                intrinsicTimeGroupId))
        WITH CLUSTERING ORDER BY (
            valueDate DESC,
            sequenceId DESC,
            timePeriod DESC,
            intrinsicTimeGroupId DESC);
        """;

    const string InsertCanonical = """
        INSERT INTO swo05_iti_query_canonical_benchmark (
            contractId, valueDate, timePeriod, intrinsicTimeMode,
            intrinsicTimeTrend, intrinsicTimeGroupId, sequenceId)
        VALUES (?, ?, ?, ?, ?, ?, ?);
        """;

    const string InsertProjection = """
        INSERT INTO swo05_iti_query_projection_v2_benchmark (
            contractId, intrinsicTimeTrend, intrinsicTimeMode, yearMonth, valueDate,
            sequenceId, timePeriod, intrinsicTimeGroupId)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?);
        """;

    // This statement deliberately remains in the benchmark assembly. Production application
    // storage must contain no ALLOW FILTERING after SWO-05 cutover.
    const string LegacyFilteredQuery = """
        SELECT max(sequenceId) AS value
        FROM swo05_iti_query_canonical_benchmark
        WHERE contractId = ?
        AND valueDate <= ?
        AND intrinsicTimeTrend = ?
        AND intrinsicTimeMode = ?
        ALLOW FILTERING;
        """;

    const string ProjectionQuery = """
        SELECT sequenceId AS value
        FROM swo05_iti_query_projection_v2_benchmark
        WHERE contractId = ?
        AND intrinsicTimeTrend = ?
        AND intrinsicTimeMode = ?
        AND yearMonth = ?
        AND valueDate <= ?
        LIMIT 1;
        """;

    static readonly string[] TimePeriods = ["OneMinute", "FiveMinutes", "FifteenMinutes", "OneHour"];
    static readonly string[] Modes =
    [
        "TrendDirectionChanged",
        "TrendExtremeChanged",
        "TrendReversalChanged",
        "TrendContinuation",
        "TrendRetracement"
    ];
    static readonly string[] Trends = ["UpTrend", "DownTrend"];

    Cluster _cluster = null!;
    ISession _session = null!;
    PreparedStatement _insertCanonical = null!;
    PreparedStatement _insertProjection = null!;
    PreparedStatement _legacyQuery = null!;
    PreparedStatement _projectionQuery = null!;
    PreparedStatement _deleteCanonical = null!;
    PreparedStatement _deleteProjection = null!;
    LocalDate _targetDate = null!;
    int _targetYearMonth;
    int[] _yearMonths = null!;
    long _expectedMaximum;

    [Params(4096, 32768)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionVariable} to a credential-free connection string for a dedicated Scylla test keyspace.");
        }

        var credentialJson = Environment.GetEnvironmentVariable(CredentialVariable);
        if (string.IsNullOrWhiteSpace(credentialJson))
            throw new InvalidOperationException($"Set {CredentialVariable} to a JSON object containing userid and password.");

        var credentials = JsonSerializer.Deserialize<CredentialDocument>(credentialJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"{CredentialVariable} contains invalid JSON.");
        if (string.IsNullOrWhiteSpace(credentials.UserId) || string.IsNullOrWhiteSpace(credentials.Password))
            throw new InvalidOperationException($"{CredentialVariable} must contain non-empty userid and password properties.");

        var connection = new CassandraConnectionStringBuilder(connectionString);
        _cluster = Cluster.Builder()
            .AddContactPoints(connection.ContactPoints)
            .WithPort(connection.Port)
            .WithCredentials(credentials.UserId, credentials.Password)
            .WithQueryTimeout(30_000)
            .WithSocketOptions(new SocketOptions().SetConnectTimeoutMillis(30_000))
            .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.LocalQuorum))
            .Build();
        _session = await _cluster.ConnectAsync(connection.DefaultKeyspace);
        using (await _session.ExecuteAsync(new SimpleStatement(CreateCanonicalTable)))
        {
        }
        using (await _session.ExecuteAsync(new SimpleStatement(CreateProjectionTable)))
        {
        }

        _insertCanonical = await _session.PrepareAsync(InsertCanonical);
        _insertProjection = await _session.PrepareAsync(InsertProjection);
        _legacyQuery = await _session.PrepareAsync(LegacyFilteredQuery);
        _projectionQuery = await _session.PrepareAsync(ProjectionQuery);
        _deleteCanonical = await _session.PrepareAsync(
            "DELETE FROM swo05_iti_query_canonical_benchmark WHERE contractId = ?;");
        _deleteProjection = await _session.PrepareAsync("""
            DELETE FROM swo05_iti_query_projection_v2_benchmark
            WHERE contractId = ? AND intrinsicTimeTrend = ? AND intrinsicTimeMode = ?
            AND yearMonth = ?;
            """);

        await CleanupRowsAsync();
        await SeedAsync();
        await VerifySeedAsync();
        await WriteQueryTraceAsync();
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        try
        {
            if (_session is not null)
                await CleanupRowsAsync();
        }
        finally
        {
            _session?.Dispose();
            _cluster?.Dispose();
        }
    }

    [Benchmark(Baseline = true, Description = "Before: canonical ALLOW FILTERING")]
    public async Task<long> LegacyFilteredTrendModeMaxSequence()
    {
        using var rows = await _session.ExecuteAsync(_legacyQuery.Bind(
            ContractId,
            _targetDate,
            TargetTrend,
            TargetMode));
        return ReadMaximum(rows);
    }

    [Benchmark(Description = "After: bounded trend/mode/month projection")]
    public async Task<long> ProjectedTrendModeMaxSequence()
    {
        using var rows = await _session.ExecuteAsync(_projectionQuery.Bind(
            ContractId,
            TargetTrend,
            TargetMode,
            _targetYearMonth,
            _targetDate));
        return ReadMaximum(rows);
    }

    async Task SeedAsync()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var targetDate = startDate.AddDays(DayCount - 1);
        _targetDate = ToLocalDate(targetDate);
        _targetYearMonth = targetDate.Year * 100 + targetDate.Month;
        _yearMonths = Enumerable.Range(0, DayCount)
            .Select(day => startDate.AddDays(day))
            .Select(static date => date.Year * 100 + date.Month)
            .Distinct()
            .ToArray();
        _expectedMaximum = 0;
        const int maximumOutstandingRequests = 64;
        var pending = new List<Task<RowSet>>(maximumOutstandingRequests);

        for (var index = 0; index < RowCount; index++)
        {
            var date = startDate.AddDays((int)((long)index * DayCount / RowCount));
            var valueDate = ToLocalDate(date);
            var yearMonth = date.Year * 100 + date.Month;
            var timePeriod = TimePeriods[index % TimePeriods.Length];
            var mode = Modes[(index / TimePeriods.Length) % Modes.Length];
            var trend = Trends[(index / (TimePeriods.Length * Modes.Length)) % Trends.Length];
            var groupId = index % 32;
            var sequenceId = index + 1L;
            if (trend == TargetTrend && mode == TargetMode)
                _expectedMaximum = sequenceId;

            pending.Add(_session.ExecuteAsync(_insertCanonical.Bind(
                ContractId,
                valueDate,
                timePeriod,
                mode,
                trend,
                groupId,
                sequenceId)));
            pending.Add(_session.ExecuteAsync(_insertProjection.Bind(
                ContractId,
                trend,
                mode,
                yearMonth,
                valueDate,
                sequenceId,
                timePeriod,
                groupId)));

            if (pending.Count >= maximumOutstandingRequests)
                await CompleteAndClearAsync(pending);
        }

        await CompleteAndClearAsync(pending);
    }

    async Task VerifySeedAsync()
    {
        var legacy = await LegacyFilteredTrendModeMaxSequence();
        var projected = await ProjectedTrendModeMaxSequence();
        if (legacy != _expectedMaximum || projected != _expectedMaximum)
        {
            throw new InvalidOperationException(
                $"SWO-05 benchmark seed verification failed: expected {_expectedMaximum}, legacy {legacy}, projection {projected}.");
        }
    }

    async Task WriteQueryTraceAsync()
    {
        await WriteTraceAsync(
            "before",
            _legacyQuery.Bind(ContractId, _targetDate, TargetTrend, TargetMode));
        await WriteTraceAsync(
            "after",
            _projectionQuery.Bind(
                ContractId, TargetTrend, TargetMode, _targetYearMonth, _targetDate));

        async Task WriteTraceAsync(string path, BoundStatement statement)
        {
            statement.EnableTracing();
            using var rows = await _session.ExecuteAsync(statement);
            _ = ReadMaximum(rows);
            var trace = rows.Info.QueryTrace;
            Console.WriteLine(
                $"SWO-05 trace path={path}, rows={RowCount}, request={trace?.RequestType}, " +
                $"durationMicros={trace?.DurationMicros}, coordinator={trace?.Coordinator}.");
            if (trace is null)
                return;
            foreach (var traceEvent in trace.Events)
                Console.WriteLine($"SWO-05 trace path={path}, event={traceEvent.Description}.");
        }
    }

    async Task CleanupRowsAsync()
    {
        using (await _session.ExecuteAsync(_deleteCanonical.Bind(ContractId)))
        {
        }
        foreach (var trend in Trends)
        {
            foreach (var mode in Modes)
            {
                foreach (var yearMonth in _yearMonths ?? [])
                {
                    using (await _session.ExecuteAsync(_deleteProjection.Bind(
                        ContractId, trend, mode, yearMonth)))
                    {
                    }
                }
            }
        }
    }

    static async Task CompleteAndClearAsync(List<Task<RowSet>> pending)
    {
        if (pending.Count == 0)
            return;

        await Task.WhenAll(pending);
        pending.Clear();
    }

    static long ReadMaximum(RowSet rows)
    {
        var row = rows.FirstOrDefault()
            ?? throw new InvalidOperationException("SWO-05 benchmark query returned no aggregate row.");
        return row.IsNull("value") ? 0 : row.GetValue<long>("value");
    }

    static LocalDate ToLocalDate(DateOnly value)
        => new(value.Year, value.Month, value.Day);

    sealed record CredentialDocument(string UserId, string Password);
}
