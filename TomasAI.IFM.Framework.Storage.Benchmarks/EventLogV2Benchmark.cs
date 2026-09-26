using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Paired, closed-loop durable-write experiment. Creates fresh databases, never uses the application's store.
/// This is a persistence-boundary benchmark, not an actor/NATS/UI or end-to-end ledger benchmark.
/// </summary>
public static class EventLogV2Benchmark
{
    internal const string AdminVariable = "IFM_EVENTLOG_BENCH_ADMIN_CONNECTION";
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    enum Variant { Baseline, V2Control, V2StreamPrimaryKey, V2BatchedMarkers, V2BatchedStreamPrimaryKey }
    static bool BatchesMarkers(Variant variant) => variant is Variant.V2BatchedMarkers or Variant.V2BatchedStreamPrimaryKey;
    static bool ConsolidatesIndexes(Variant variant) => variant is Variant.V2StreamPrimaryKey or Variant.V2BatchedStreamPrimaryKey;
    sealed record Scenario(string Name, EventLogWriteMode Mode, bool Audit, bool Markers,
        int Streams, int Events, bool Financial = false, int MarkerEvery = 1, int QueueCapacity = 8192, int SoakSeconds = 0);
    sealed record Sample(string Scenario, string Variant, int Repetition, int RunOrder, string Database,
        int Commands, int Events, double Seconds, double CommandsPerSecond, double EventsPerSecond,
        double P50Ms, double P95Ms, double P99Ms, double MaxMs, long AllocatedBytes,
        int Gen0, int Gen1, int Gen2, double ClientCpuMs, long WalBytes, long EventTableBytes,
        long Transactions, long MaximumQueueDepth, long MarkerCommands, long MarkerRows,
        double MarkerAwaitMs, double ReplayMs, long ReplayEvents, bool Verified, int BlockedAdmissionLowerBound = 0,
        long SeedEvents = 0, long SeedTableBytes = 0);
    static readonly List<Sample> Results = [];
    static readonly DateTime FixtureTime = new(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc);
    static readonly string Payload = CreatePayload();
    static readonly CommandAuditMessagePackCodec AuditCodec = new();
    static string _output = "";
    static string _admin = "";
    static int _rounds;
    static int _seedRounds;
    static int _batchEvents = 256;

    internal static async Task RunAsync(string[] args)
    {
        var allowed = new[] { "--quick", "--marker-experiment", "--mixed-marker-experiment", "--pressure-experiment", "--retained-experiment", "--schema-batched-experiment", "--soak-seconds=", "--repeats=", "--rounds=", "--seed-rounds=", "--batch-events=", "--output=" };
        if (args.Any(a => !allowed.Any(k => k.EndsWith('=') ? a.StartsWith(k, StringComparison.Ordinal) : a == k)))
            throw new ArgumentException("Options: --quick --marker-experiment --mixed-marker-experiment --pressure-experiment --retained-experiment --soak-seconds=N --repeats=N --rounds=N --seed-rounds=N --batch-events=N --output=PATH");
        var raw = Environment.GetEnvironmentVariable(AdminVariable)
            ?? throw new InvalidOperationException($"Set {AdminVariable} to an isolated loopback PostgreSQL server (not port 5432).");
        var adminBuilder = new NpgsqlConnectionStringBuilder(raw);
        if (adminBuilder.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            adminBuilder.Port == 5432 || adminBuilder.Database != "postgres")
            throw new InvalidOperationException("Use a dedicated loopback server, non-5432 port, and database postgres.");
        _admin = raw;
        var quick = args.Contains("--quick");
        var mixedMarkers = args.Contains("--mixed-marker-experiment");
        var pressure = args.Contains("--pressure-experiment");
        var retained = args.Contains("--retained-experiment");
        var timed = pressure || retained;
        var schemaBatched = args.Contains("--schema-batched-experiment");
        if (schemaBatched && (pressure || mixedMarkers || args.Contains("--marker-experiment") || quick))
            throw new ArgumentException("Schema-batched experiment can combine only with retained-history mode.");
        var soakSeconds = Number(args, "--soak-seconds=", 60, 10, 600);
        if (timed && (mixedMarkers || args.Contains("--marker-experiment") || quick || (pressure && retained)))
            throw new ArgumentException("Timed experiments cannot be combined with other experiment modes.");
        var markerExperiment = args.Contains("--marker-experiment") || mixedMarkers;
        var repeats = Number(args, "--repeats=", timed ? 4 : quick ? 3 : 6, 2, 30);
        _rounds = Number(args, "--rounds=", quick ? 4 : 32, 1, 10000);
        _seedRounds = Number(args, "--seed-rounds=", quick ? 2 : 8, 1, 10000);
        _batchEvents = Number(args, "--batch-events=", 256, 1, 256);
        var runId = Guid.NewGuid().ToString("N")[..12];
        _output = Path.GetFullPath(args.FirstOrDefault(a => a.StartsWith("--output="))?["--output=".Length..]
            ?? Path.Combine("BenchmarkDotNet.Artifacts", "event-log-v2", runId));
        if (Directory.Exists(_output) && Directory.EnumerateFileSystemEntries(_output).Any())
            throw new InvalidOperationException("Output directory must be new or empty; existing results will not be overwritten.");
        Directory.CreateDirectory(_output);
        Results.Clear();
        await using var admin = new NpgsqlConnection(_admin);
        await admin.OpenAsync();
        var databases = Convert.ToInt64(await Scalar(admin,
            "SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres'"));
        if (databases != 0)
            throw new InvalidOperationException("Benchmark server must be empty. Existing databases are never reused or removed.");
        if (timed) await EventLogProcessQualification.ValidateContainer(_admin);
        var durable = (string)(await Scalar(admin,
            "SELECT current_setting('fsync') || '/' || current_setting('synchronous_commit') || '/' || current_setting('full_page_writes')"))!;
        if (durable != "on/on/on") throw new InvalidOperationException("Durability must be on/on/on.");
        var version = (string)(await Scalar(admin, "SELECT version()"))!;
        var metadata = new
        {
            RunId = runId, StartedUtc = DateTime.UtcNow, PostgreSql = version, Durability = durable,
            SchemaBatchedExperiment = schemaBatched,
            SchemaComparison = schemaBatched ? "Both variants batch markers on event_log; four-index control vs three-index stream primary key. All other protections retained." : null,
            Experiment = retained && schemaBatched ? "Retained-history timed index consolidation; both writers batch markers"
                : retained ? "Retained-history timed load; 8192-capacity queue; unchanged four-index schema" : pressure ? "Bounded queue pressure and timed soak; unchanged four-index schema" : mixedMarkers ? "Mixed marker density; unchanged four-index schema" : markerExperiment ? "Set-based markers; unchanged four-index schema" : "Index consolidation",
            MarkerPattern = schemaBatched && !retained ? "schema-none64: none; schema-mixed8: every eighth version; schema-all64: every event"
                : timed ? "Every eighth stream version; eight events per command; one marker per command"
                : mixedMarkers ? "Per-stream version modulo N; 0%, 1/64, 1/8, 1/2, 100%; single-event commands burst every eighth version" : "All or no events",
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            CpuCount = Environment.ProcessorCount, ServerGC = GCSettings.IsServerGC,
            Repetitions = repeats, RoundsPerStream = timed ? (int?)null : _rounds, SeedRoundsPerStream = _seedRounds,
            SoakSeconds = timed ? soakSeconds : 0,
            PayloadCharacters = Payload.Length, PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Payload))),
            Compression = true, QueueCapacity = pressure ? 8 : 8192, BatchEvents = _batchEvents, BatchBytes = 1048576, BatchDelayMs = 1,
            Limitations = retained && schemaBatched
                ? "Same event_log table name and marker batching in both variants; only index layout differs. Retained synthetic history; 64 bounded producers; observer overhead included. Unequal measured work and final table size in timed windows. Not production migration, crash or full-day leak qualification."
                : retained
                ? "64 bounded producers, fixed synthetic payload, retained seed through real writer; not actual production distribution or full-day leak proof. No queue saturation claim. Observer overhead included; container I/O cumulative, not disk latency. No identical-table control in this timed pair."
                : pressure
                ? "64 bounded closed-loop producers, one pending command/stream; small test queue, not production queue tuning. One-second client/PG wait observations and periodic docker stats include observer overhead. Docker block I/O is cumulative container accounting, not disk latency. Warm replay; synthetic payload; not end-to-end actor/projector or long-duration leak proof."
                : "Closed-loop persistence boundary; synthetic payload; warm replay; not production promotion evidence. PostgreSQL WAL includes server background work. No server CPU/disk sampler or crash-injection qualification.",
            SourceHashes = SourceHashes()
        };
        await File.WriteAllTextAsync(Path.Combine(_output, "metadata.json"), JsonSerializer.Serialize(metadata, Json));
        Scenario[] scenarios = quick
            ? [new("atomic-1", EventLogWriteMode.BinaryCopy, true, false, 16, 1),
               new("atomic-markers-64", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true)]
            : [new("regular-sequential-1", EventLogWriteMode.Sequential, false, false, 64, 1),
               new("regular-copy-1", EventLogWriteMode.BinaryCopy, false, false, 64, 1),
               new("atomic-1", EventLogWriteMode.BinaryCopy, true, false, 64, 1),
               new("atomic-hot-stream-1", EventLogWriteMode.BinaryCopy, true, false, 1, 1),
               new("atomic-markers-64", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true),
               new("atomic-batch-256", EventLogWriteMode.BinaryCopy, true, false, 4, 256)];
        if (markerExperiment)
            scenarios = quick
                ? [new("no-markers-1", EventLogWriteMode.BinaryCopy, true, false, 8, 1, true),
                   new("markers-1", EventLogWriteMode.BinaryCopy, true, true, 8, 1, true),
                   new("markers-64", EventLogWriteMode.BinaryCopy, true, true, 2, 64, true)]
                : [new("no-markers-1", EventLogWriteMode.BinaryCopy, true, false, 64, 1, true),
                   new("markers-1", EventLogWriteMode.BinaryCopy, true, true, 64, 1, true),
                   new("markers-hot-1", EventLogWriteMode.BinaryCopy, true, true, 1, 1, true),
                   new("no-markers-64", EventLogWriteMode.BinaryCopy, true, false, 4, 64, true),
                   new("markers-64", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true)];
        if (mixedMarkers)
            scenarios = [new("mix-none", EventLogWriteMode.BinaryCopy, true, false, 4, 64, true),
                new("mix-1of64", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true, 64),
                new("mix-1of8", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true, 8),
                new("mix-half", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true, 2),
                new("mix-all", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true),
                new("mix-burst", EventLogWriteMode.BinaryCopy, true, true, 64, 1, true, 8)];
        Variant[] variants = markerExperiment
            ? [Variant.Baseline, Variant.V2Control, Variant.V2BatchedMarkers]
            : [Variant.Baseline, Variant.V2Control, Variant.V2StreamPrimaryKey];
        if (timed)
        {
            scenarios = [new(retained ? "retained-8" : "pressure-8", EventLogWriteMode.BinaryCopy, true, true, 64, 8, true, 8, retained ? 8192 : 8, soakSeconds)];
            variants = [Variant.Baseline, Variant.V2BatchedMarkers];
        }
        if (schemaBatched)
        {
            scenarios = retained
                ? [new("schema-retained8", EventLogWriteMode.BinaryCopy, true, true, 64, 8, true, 8, 8192, soakSeconds)]
                : [new("schema-none64", EventLogWriteMode.BinaryCopy, true, false, 4, 64, true),
                new("schema-mixed8", EventLogWriteMode.BinaryCopy, true, true, 64, 8, true, 8),
                new("schema-all64", EventLogWriteMode.BinaryCopy, true, true, 4, 64, true)];
            variants = [Variant.V2BatchedMarkers, Variant.V2BatchedStreamPrimaryKey];
        }
        try
        {
            foreach (var scenario in scenarios)
            for (var repeat = 0; repeat < repeats; repeat++)
            {
                // Rotate paired order to avoid always giving the warmed host to the candidate.
                for (var order = 0; order < variants.Length; order++)
                {
                    var variant = variants[(order + repeat) % variants.Length];
                    var suffix = $"{scenario.Name.Replace('-', '_')}_{repeat}_{(int)variant}";
                    var database = $"ifm_eventlog_bench_{runId}_{suffix}";
                    if (database.Length > 63) throw new InvalidOperationException("Database identifier exceeds PostgreSQL limit.");
                    Console.WriteLine($"Running {scenario.Name} / {variant} / repeat {repeat + 1}");
                    var sample = await RunSample(admin, database, scenario, variant, repeat + 1, order + 1);
                    Results.Add(sample);
                    await WriteResults();
                    Console.WriteLine($"  {sample.CommandsPerSecond:F1} commands/s; p99 {sample.P99Ms:F2} ms; verification passed");
                }
            }
        }
        finally { await WriteResults(); }
        Console.WriteLine($"Results: {_output}");
    }

    static async Task<Sample> RunSample(NpgsqlConnection admin, string database, Scenario scenario,
        Variant variant, int repetition, int order)
    {
        // Only generated identifiers can reach DDL. No user-supplied table/database identifiers.
        var builder = new NpgsqlConnectionStringBuilder(_admin) { Database = database, Pooling = true };
        var directConnectionString = builder.ConnectionString;
        builder.Username = string.Empty;
        builder.Password = string.Empty;
        var connectionString = builder.ConnectionString;
        var layout = EventLogSqlLayout.ForBenchmark(connectionString, BatchesMarkers(variant));
        await Execute(admin, $"CREATE DATABASE \"{database}\"");
        var success = false;
        try
        {
            var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
                connectionString, "System.Data.Postgres");
            await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
            await using var connection = new NpgsqlConnection(directConnectionString);
            await connection.OpenAsync();
            await Execute(connection, PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01);
            if (variant != Variant.Baseline)
                await Execute(connection, "ALTER TABLE event_log RENAME TO event_log");
            if (ConsolidatesIndexes(variant))
                await Execute(connection, """
                    ALTER TABLE event_log DROP CONSTRAINT event_log_pkey;
                    ALTER TABLE event_log ADD CONSTRAINT event_log_pkey
                        PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
                    """);
            var table = variant == Variant.Baseline ? "event_log" : "event_log";
            await VerifyShape(connection, table, variant);
            await File.WriteAllTextAsync(Path.Combine(_output, database + "-indexes.txt"),
                (string)(await Scalar(connection,
                    "SELECT string_agg(indexdef,chr(10) ORDER BY indexname) FROM pg_indexes WHERE schemaname='public' AND tablename=$1", table))!);
            var streams = new (string Name, long Id)[scenario.Streams];
            for (var i = 0; i < streams.Length; i++)
            {
                var name = scenario.Financial ? $"Command.FundCommand.{i + 1}" : $"Benchmark.Stream.{i + 1}";
                streams[i] = (name, Convert.ToInt64(await Scalar(connection,
                    "INSERT INTO event_stream_id(eventstream) VALUES($1) RETURNING eventstreamid", name)));
            }
            var eventNameId = Convert.ToInt32(await Scalar(connection,
                "INSERT INTO event_name_id(eventname,eventtypename) VALUES($1,$2) RETURNING eventnameid",
                nameof(BenchmarkEvent), typeof(BenchmarkEvent).AssemblyQualifiedName!));
            var versions = new long[streams.Length];
            await using var appender = CreateAppender(connectionString, scenario, layout);
            // Seed through the real durable writer, not a lighter SQL shortcut. Includes serializer/JIT warmup.
            await Workload(appender, scenario, streams, versions, eventNameId, _seedRounds);
            var seedEvents = versions.Sum();
            if (Convert.ToInt64(await Scalar(connection, $"SELECT count(*) FROM {table}")) != seedEvents)
                throw new InvalidOperationException("Retained seed event count mismatch.");
            var seedBytes = Convert.ToInt64(await Scalar(connection, "SELECT pg_total_relation_size($1::regclass)", table));
            Console.WriteLine($"  Seed verified: {seedEvents} events, {seedBytes} event-table/index bytes.");
            var blockedLowerBound = scenario.SoakSeconds > 0 && scenario.QueueCapacity < scenario.Streams
                ? await VerifyBackpressure(connection, appender, scenario, streams, versions, eventNameId) : 0;
            await Execute(connection, "ANALYZE");
            var measuredStartVersions = versions.ToArray();
            using var metrics = new BatchMetrics();
            await using var observer = scenario.SoakSeconds > 0 ? new EventLogSoakObserver(directConnectionString,
                Path.Combine(_output, database + "-observations.json"),
                () => metrics.CurrentQueueDepth, () => metrics.Transactions) : null;
            var walBefore = (string)(await Scalar(connection, "SELECT pg_current_wal_insert_lsn()::text"))!;
            var gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var allocated = GC.GetTotalAllocatedBytes(true);
            using var process = Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            var timer = Stopwatch.StartNew();
            var latencies = scenario.SoakSeconds > 0
                ? await TimedWorkload(appender, scenario, streams, versions, eventNameId)
                : await Workload(appender, scenario, streams, versions, eventNameId, _rounds);
            timer.Stop();
            var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
            var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
            var collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gc[i]).ToArray();
            // Drain the consumer so its final commit measurement cannot race result collection.
            await appender.DisposeAsync();
            if (scenario.SoakSeconds > 0 && metrics.CurrentQueueDepth != 0)
                throw new InvalidOperationException("Outstanding queue/admission work did not drain to zero.");
            metrics.Dispose();
            if (observer is not null) await observer.DisposeAsync();
            var transactions = metrics.Transactions;
            var queueDepth = metrics.MaximumQueueDepth;
            var markerCommands = metrics.MarkerCommands;
            var markerRows = metrics.MarkerRows;
            var markerAwaitMs = metrics.MarkerAwaitMs;
            var expectedMarkers = scenario.Markers ? versions.Sum(v => v / scenario.MarkerEvery) -
                measuredStartVersions.Sum(v => v / scenario.MarkerEvery) : 0;
            if (markerRows != expectedMarkers ||
                (!BatchesMarkers(variant) && markerCommands != markerRows) ||
                (BatchesMarkers(variant) && (markerCommands > transactions || markerCommands > markerRows ||
                    (markerRows > 0 && markerCommands == 0) ||
                    (scenario.Markers && scenario.MarkerEvery == 1 && markerCommands != transactions))))
                throw new InvalidOperationException("Marker telemetry does not match the measured workload.");
            var walBytes = Convert.ToInt64(await Scalar(connection,
                "SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn)::bigint", walBefore));
            var total = versions.Sum();
            var replay = await VerifyAndReplay(connection, table, scenario, total, versions);
            var bytes = Convert.ToInt64(await Scalar(connection, "SELECT pg_total_relation_size($1::regclass)", table));
            await using var verificationAppender = CreateAppender(connectionString, scenario, layout);
            await VerifyFailures(connection, verificationAppender, scenario, layout, table, streams[0], versions[0], eventNameId);
            await EventLogMarkerVerification.VerifyAsync(connection, verificationAppender, layout, table, eventNameId);
            var commands = latencies.Length;
            Array.Sort(latencies);
            var result = new Sample(scenario.Name, variant.ToString(), repetition, order, database, commands,
                commands * scenario.Events, timer.Elapsed.TotalSeconds, commands / timer.Elapsed.TotalSeconds,
                commands * scenario.Events / timer.Elapsed.TotalSeconds, Percentile(latencies, .50),
                Percentile(latencies, .95), Percentile(latencies, .99), latencies[^1], allocatedBytes,
                collections[0], collections[1], collections[2], cpuMs, walBytes, bytes,
                transactions, queueDepth, markerCommands, markerRows, markerAwaitMs, replay.Milliseconds, replay.Events, true, blockedLowerBound,
                seedEvents, seedBytes);
            success = true;
            return result;
        }
        finally
        {
            // Preserve failed fixtures for diagnosis. Successful fixtures are disposable and uniquely owned by this run.
            if (success)
                await Execute(admin, $"DROP DATABASE \"{database}\" WITH (FORCE)");
            else
                Console.Error.WriteLine($"Retained failed isolated fixture: {database}");
        }
    }

    static IEventLogAppender CreateAppender(string connection, Scenario scenario, EventLogSqlLayout layout)
    {
        var options = new EventLogPersistenceOptions { WriteMode = scenario.Mode, UseLz4Compression = true,
            MaximumEventsPerBatch = _batchEvents,
            QueueCommandCapacity = scenario.QueueCapacity };
        return scenario.Mode == EventLogWriteMode.Sequential
            ? new SequentialEventLogAppender(connection, true, options, layout)
            : new BinaryCopyEventLogAppender(connection, true, options, layout);
    }

    static async Task<double[]> Workload(IEventLogAppender appender, Scenario scenario,
        (string Name, long Id)[] streams, long[] versions, int eventNameId, int rounds)
    {
        var timings = new double[streams.Length * rounds];
        await Task.WhenAll(streams.Select(async (stream, index) =>
        {
            for (var round = 0; round < rounds; round++)
            {
                var started = Stopwatch.GetTimestamp();
                var request = Request(stream, versions[index], eventNameId, scenario);
                var result = await appender.AppendAsync(request);
                if (result.Assignments.Count != scenario.Events ||
                    result.Assignments[^1].StreamVersion != versions[index] + scenario.Events)
                    throw new InvalidOperationException("Appender returned incorrect stream assignments.");
                // Actor-like state only advances after a confirmed durable acknowledgment.
                versions[index] += scenario.Events;
                timings[index * rounds + round] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
        }));
        return timings;
    }

    static async Task<int> VerifyBackpressure(NpgsqlConnection connection, IEventLogAppender appender,
        Scenario scenario, (string Name, long Id)[] streams, long[] versions, int eventNameId)
    {
        using var metrics = new BatchMetrics();
        await using var transaction = await connection.BeginTransactionAsync();
        await Execute(connection, "SELECT eventstreamid FROM event_stream_id ORDER BY eventstreamid FOR UPDATE");
        // Workload enumerates all bounded producers before returning its incomplete task.
        var pending = Workload(appender, scenario, streams, versions, eventNameId, 1);
        var timer = Stopwatch.StartNew();
        int lowerBound;
        try
        {
            while (metrics.QueuedRequests != scenario.Streams || Convert.ToInt64(await Scalar(connection,
                "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'")) == 0)
            {
                if (timer.Elapsed > TimeSpan.FromSeconds(2)) throw new TimeoutException("Backpressure barrier not observed.");
                await Task.Delay(10);
            }
            // Metric includes blocked admission and channel contents, excludes the transaction batch;
            // allow one extra consumer carry item conservatively. More than capacity+carry must wait.
            lowerBound = checked((int)metrics.CurrentQueueDepth - scenario.QueueCapacity - 1);
            if (lowerBound <= 0 || pending.IsCompleted)
                throw new InvalidOperationException("No proven blocked channel admission at the pressure barrier.");
        }
        finally { await transaction.RollbackAsync(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(20));
        return lowerBound;
    }

    static async Task<double[]> TimedWorkload(IEventLogAppender appender, Scenario scenario,
        (string Name, long Id)[] streams, long[] versions, int eventNameId)
    {
        var timer = Stopwatch.StartNew();
        var workers = streams.Select(async (stream, index) =>
        {
            var timings = new List<double>();
            while (timer.Elapsed.TotalSeconds < scenario.SoakSeconds)
            {
                if (timings.Count >= 4096) throw new InvalidOperationException("Soak safety cap reached before its deadline.");
                var started = Stopwatch.GetTimestamp();
                var result = await appender.AppendAsync(Request(stream, versions[index], eventNameId, scenario));
                if (result.Assignments.Count != scenario.Events ||
                    result.Assignments[^1].StreamVersion != versions[index] + scenario.Events)
                    throw new InvalidOperationException("Timed append returned invalid stream assignments.");
                versions[index] += scenario.Events;
                timings.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            return timings;
        });
        var completed = await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(scenario.SoakSeconds + 30));
        return completed.SelectMany(t => t).ToArray();
    }

    static EventLogAppendRequest Request((string Name, long Id) stream, long expected, int eventNameId,
        Scenario scenario, Guid? commandId = null)
    {
        var id = commandId ?? Guid.NewGuid();
        var command = new BenchmarkCommand { CommandId = id, StreamId = stream.Name, Value = expected, Payload = Payload };
        var entries = Enumerable.Range(0, scenario.Events).Select(i => new EventLogAppendEntry(eventNameId,
            new BenchmarkEvent { CommandId = id, AggregateId = stream.Name, Value = expected + i + 1,
                Payload = Payload, RequiresDurableProjection = scenario.Markers && (expected + i + 1) % scenario.MarkerEvery == 0 })).ToArray();
        return new EventLogAppendRequest(stream.Name, stream.Id, id, entries, expected, FixtureTime,
            scenario.Audit ? CommandAuditEnvelope.Create(command, AuditCodec) : null);
    }

    static async Task VerifyShape(NpgsqlConnection connection, string table, Variant variant)
    {
        var indexes = Convert.ToInt32(await Scalar(connection,
            "SELECT count(*) FROM pg_index WHERE indrelid=$1::regclass", table));
        if (indexes != (ConsolidatesIndexes(variant) ? 3 : 4))
            throw new InvalidOperationException($"Unexpected {table} index count: {indexes}.");
        var primaryKey = (string)(await Scalar(connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid=$1::regclass AND contype='p'", table))!;
        var expectedPrimaryKey = ConsolidatesIndexes(variant)
            ? "PRIMARY KEY (eventstreamid, streamversion)" : "PRIMARY KEY (eventstreamid, eventnameid, eventversion)";
        if (primaryKey != expectedPrimaryKey) throw new InvalidOperationException("Unexpected event-log primary key: " + primaryKey);

        var fks = Convert.ToInt32(await Scalar(connection,
            "SELECT count(*) FROM pg_constraint WHERE contype='f' AND confrelid=$1::regclass", table));
        if (fks < 5) throw new InvalidOperationException($"Missing event identity foreign keys: {fks}.");
    }

    static async Task<(double Milliseconds, long Events)> VerifyAndReplay(NpgsqlConnection connection,
        string table, Scenario scenario, long expectedEvents, long[] versions)
    {
        var events = Convert.ToInt64(await Scalar(connection, $"SELECT count(*) FROM {table}"));
        var audits = Convert.ToInt64(await Scalar(connection, "SELECT count(*) FROM command_log"));
        var markers = Convert.ToInt64(await Scalar(connection, "SELECT count(*) FROM event_projector_state"));
        if (events != expectedEvents || audits != (scenario.Audit ? expectedEvents / scenario.Events : 0) ||
            markers != (scenario.Markers ? versions.Sum(v => v / scenario.MarkerEvery) : 0))
            throw new InvalidOperationException($"Durable counts mismatch: events={events}, audits={audits}, markers={markers}.");
        var misplacedMarkers = Convert.ToInt64(await Scalar(connection, $"""
            SELECT count(*) FROM {table} e LEFT JOIN event_projector_state p ON p.eventid=e.eventversion
            WHERE (p.eventid IS NOT NULL) <> ($1 AND e.streamversion % $2 = 0)
                OR (p.eventid IS NOT NULL AND (p.streamversion<>e.streamversion OR p.eventstreamid<>e.eventstreamid))
            """, scenario.Markers, scenario.MarkerEvery));
        if (misplacedMarkers != 0) throw new InvalidOperationException("Durable marker density/identity mismatch.");
        var invalid = Convert.ToInt64(await Scalar(connection, $"""
            SELECT count(*) FROM event_stream_id s
            LEFT JOIN (SELECT eventstreamid, count(*) n, min(streamversion) first, max(streamversion) last
                FROM {table} GROUP BY eventstreamid) e USING(eventstreamid)
            WHERE e.n IS NULL OR e.first<>1 OR e.n<>e.last OR s.currentversion<>e.last
            """));
        if (invalid != 0) throw new InvalidOperationException("Invalid durable stream versions.");
        var codec = new EventLogMessagePackCodec();
        var timer = Stopwatch.StartNew();
        long count = 0;
        var seen = new Dictionary<long, long>();
        await using var read = new NpgsqlCommand(
            $"SELECT eventstreamid,streamversion,eventversion,eventpayload FROM {table} ORDER BY eventstreamid,streamversion", connection);
        await using var reader = await read.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stream = reader.GetInt64(0);
            var streamVersion = reader.GetInt64(1);
            var next = seen.GetValueOrDefault(stream) + 1;
            var value = (BenchmarkEvent)codec.Deserialize(typeof(BenchmarkEvent).AssemblyQualifiedName!,
                reader.GetInt64(2), reader.GetFieldValue<byte[]>(3));
            if (next != streamVersion || value.Value != next || value.Payload != Payload ||
                value.RequiresDurableProjection != (scenario.Markers && next % scenario.MarkerEvery == 0))
                throw new InvalidOperationException("Replay payload/order mismatch.");
            seen[stream] = next;
            count++;
        }
        if (count != expectedEvents || !seen.Values.Order().SequenceEqual(versions.Order()))
            throw new InvalidOperationException("Replay did not rebuild the expected in-memory state.");
        return (timer.Elapsed.TotalMilliseconds, count);
    }

    static async Task VerifyFailures(NpgsqlConnection connection, IEventLogAppender appender, Scenario scenario,
        EventLogSqlLayout layout, string table, (string Name, long Id) stream, long version, int eventNameId)
    {
        var before = await Snapshot(connection, table);
        // A stale expected version must roll back command reservation, events, counter, and markers together.
        await MustFail(() => appender.AppendAsync(Request(stream, version - 1, eventNameId, scenario)).AsTask(),
            e => e is TomasAI.IFM.Shared.Exceptions.ConcurrencyException);
        if (before != await Snapshot(connection, table)) throw new InvalidOperationException("Stale write left durable side effects.");
        if (scenario.Audit)
        {
            var command = Guid.NewGuid();
            var request = Request(stream, version, eventNameId, scenario, command);
            await appender.AppendAsync(request);
            var committed = await Snapshot(connection, table);
            await MustFail(() => appender.AppendAsync(request).AsTask(), e => e is CommandAuditDuplicateException);
            if (committed != await Snapshot(connection, table)) throw new InvalidOperationException("Duplicate appended twice.");
            var conflicting = request with { CommandAudit = CommandAuditEnvelope.Create(
                new BenchmarkCommand { CommandId = command, StreamId = stream.Name, Value = -100, Payload = Payload }, AuditCodec) };
            // Current atomic COPY writer reports both hash conflicts and exact duplicates as DuplicateException.
            // The benchmark checks rejection and unchanged durable state, not a diagnostic distinction it lacks.
            await MustFail(() => appender.AppendAsync(conflicting).AsTask(),
                e => e is CommandAuditPayloadConflictException or CommandAuditDuplicateException);
            if (committed != await Snapshot(connection, table)) throw new InvalidOperationException("Hash conflict changed durable state.");
            version += scenario.Events;
            // A second appender has no in-memory duplicate cache.
            var restartConnection = new NpgsqlConnectionStringBuilder(connection.ConnectionString)
                { Username = string.Empty, Password = string.Empty };
            await using var restarted = CreateAppender(restartConnection.ConnectionString, scenario, layout);
            await MustFail(() => restarted.AppendAsync(request).AsTask(), e => e is CommandAuditDuplicateException);
        }
    }

    static async Task<string> Snapshot(NpgsqlConnection connection, string table) =>
        (string)(await Scalar(connection, $"""
            SELECT (SELECT count(*) FROM {table})::text || '/' ||
                (SELECT count(*) FROM command_log)::text || '/' ||
                (SELECT count(*) FROM event_projector_state)::text || '/' ||
                (SELECT coalesce(sum(currentversion),0) FROM event_stream_id)::text
            """))!;

    static async Task MustFail(Func<Task> action, Func<Exception, bool> expected)
    {
        try { await action(); }
        catch (Exception e) when (expected(e)) { return; }
        throw new InvalidOperationException("Expected failure was not observed.");
    }

    static async Task WriteResults()
    {
        if (_output.Length == 0) return;
        await File.WriteAllTextAsync(Path.Combine(_output, "samples.json"), JsonSerializer.Serialize(Results, Json));
        var text = new StringBuilder("# Isolated event-log v2 benchmark\n\n");
        text.AppendLine("Closed-loop persistence-boundary pilot. All reported samples passed durable-count, replay, duplicate (audited path), stale-version, and financial-fence checks. No production migration is approved by this report.\n");
        text.AppendLine("| Scenario | Variant | Repeats | Median commands/s | Median p99 ms | Median allocated bytes/command | Median WAL bytes/event |");
        text.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var group in Results.GroupBy(r => (r.Scenario, r.Variant)))
        {
            text.AppendLine(FormattableString.Invariant($"| {group.Key.Scenario} | {group.Key.Variant} | {group.Count()} | {Median(group.Select(r => r.CommandsPerSecond)):F1} | {Median(group.Select(r => r.P99Ms)):F2} | {Median(group.Select(r => (double)r.AllocatedBytes / r.Commands)):F0} | {Median(group.Select(r => (double)r.WalBytes / r.Events)):F0} |"));
        }
        text.AppendLine("\n## Paired throughput comparisons\n");
        foreach (var scenario in Results.Select(r => r.Scenario).Distinct())
        {
            var schema = Results.Any(r => r.Scenario == scenario && r.Variant == nameof(Variant.V2BatchedStreamPrimaryKey));
            var baseline = Results.Where(r => r.Scenario == scenario && r.Variant == (schema ? nameof(Variant.V2BatchedMarkers) : nameof(Variant.Baseline))).ToDictionary(r => r.Repetition);
            foreach (var variant in schema ? new[] { Variant.V2BatchedStreamPrimaryKey } : new[] { Variant.V2Control, Variant.V2StreamPrimaryKey, Variant.V2BatchedMarkers })
            {
                var differences = Results.Where(r => r.Scenario == scenario && r.Variant == variant.ToString() && baseline.ContainsKey(r.Repetition))
                    .Select(r => 100 * (r.CommandsPerSecond / baseline[r.Repetition].CommandsPerSecond - 1)).ToArray();
                if (differences.Length > 0)
                    text.AppendLine(FormattableString.Invariant($"- {scenario}, {variant}: median {Median(differences):F1}% (paired range {differences.Min():F1}% to {differences.Max():F1}%)."));
            }
        }
        text.AppendLine("\n## Marker statement counts (median per sample)\n");
        foreach (var group in Results.GroupBy(r => (r.Scenario, r.Variant)))
            text.AppendLine(FormattableString.Invariant($"- {group.Key.Scenario}, {group.Key.Variant}: {Median(group.Select(r => (double)r.MarkerCommands)):F0} statements, {Median(group.Select(r => (double)r.MarkerRows)):F0} rows; {Median(group.Select(r => r.MarkerAwaitMs)):F2} ms cumulative marker operation time."));
        if (Results.Any(r => r.Variant == nameof(Variant.V2BatchedStreamPrimaryKey)))
            text.AppendLine("\nSchema-only paired comparison: both variants use event_log and batch projection markers. The control has four indexes; the candidate has a stream/version primary key and three indexes. Global event identity, command lookup, financial fence, timestamp type and durable auditing remain unchanged. Gains are relative to the already-batched control, not the original unbatched writer. Synthetic storage-boundary measurements; not production migration approval.");
        else if (Results.Any(r => r.Scenario == "retained-8"))
            text.AppendLine("\nRetained-history timed comparison; no identical-table control. Each fixture verifies seed count and captures seed table/index bytes before measurement. Queue capacity is 8192 with 64 bounded producers: no saturation or backpressure claim. Final outstanding queue/admission depth must be zero. Observations include client memory, PG wait snapshots and container CPU/I/O; observer overhead is included. Synthetic workload, not production-sized history or full-day leak/Gen 2 qualification.");
        else if (Results.Any(r => r.BlockedAdmissionLowerBound > 0))
            text.AppendLine("\nTimed bounded pressure comparison; no identical-v2 control in this run. Each sample proved blocked admission before timing and zero outstanding queue/admission work after drain. Queue depth includes channel contents and admission waiters, not the in-flight transaction batch. Per-fixture observation files include client memory, PostgreSQL wait snapshots and periodic container CPU/I/O. Observer overhead is included; container I/O is cumulative accounting, not disk latency. This is not long-duration leak or end-to-end actor/projector qualification.");
        else
            text.AppendLine("\nAn identical v2 control estimates naming/run noise. Mixed-sign gains or gains comparable to control variation are inconclusive. Tail percentiles from small samples are descriptive only. Raw samples and metadata include run order, WAL, GC, client CPU, queue depth, measured commits, table size, and warm replay. Server CPU, disk latency, crash/ack-loss injection, snapshot recovery, cross-process races, concurrent replay, and production-like dataset scale remain separate qualification work.");
        await File.WriteAllTextAsync(Path.Combine(_output, "summary.md"), text.ToString());
    }

    static async Task<object?> Scalar(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync();
    }

    static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    static int Number(string[] args, string key, int fallback, int min, int max)
    {
        var supplied = args.Where(a => a.StartsWith(key, StringComparison.Ordinal)).ToArray();
        if (supplied.Length > 1) throw new ArgumentException($"Duplicate {key}");
        var value = supplied.Length == 0 ? fallback : int.Parse(supplied[0][key.Length..], CultureInfo.InvariantCulture);
        return value >= min && value <= max ? value : throw new ArgumentOutOfRangeException(key);
    }

    internal static double Percentile(double[] sorted, double fraction) =>
        sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * fraction) - 1, 0, sorted.Length - 1)];
    static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }
    static string CreatePayload()
    {
        var bytes = new byte[768];
        new Random(731).NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
    static Dictionary<string, string> SourceHashes()
    {
        string[] files = [
            "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogV2Benchmark.cs",
            "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogSoakObserver.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/BinaryCopyEventLogAppender.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/SequentialEventLogAppender.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/BatchedProjectionMarkerWriter.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogAppenderSupport.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogSqlLayout.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogPersistenceMetrics.cs",
            "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogMarkerVerification.cs",
            "TomasAI.IFM.Application.Storage/EventSourceDb/Schema/EventSourceSchemaSql.cs",
            "TomasAI.IFM.Application.Storage/PortfolioDb/PortfolioDbSql.cs"];
        return files.ToDictionary(p => p, p => File.Exists(p) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) : "unavailable");
    }

    sealed class BatchMetrics : IDisposable
    {
        readonly MeterListener _listener = new();
        long _transactions, _depth, _maximum, _queuedRequests;
        internal long QueuedRequests => Interlocked.Read(ref _queuedRequests);
        internal long CurrentQueueDepth => Interlocked.Read(ref _depth);
        long _markerCommands, _markerRows;
        double _markerAwaitMs;
        readonly object _markerLock = new();
        internal long MarkerCommands => Interlocked.Read(ref _markerCommands);
        internal long MarkerRows => Interlocked.Read(ref _markerRows);
        internal double MarkerAwaitMs { get { lock (_markerLock) return _markerAwaitMs; } }
        internal long Transactions => Interlocked.Read(ref _transactions);
        internal long MaximumQueueDepth => Interlocked.Read(ref _maximum);
        internal BatchMetrics()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "TomasAI.IFM.EventLogPersistence")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            {
                if (instrument.Name == "ifm.event_log.marker.commands") Interlocked.Add(ref _markerCommands, value);
                if (instrument.Name == "ifm.event_log.marker.rows") Interlocked.Add(ref _markerRows, value);
                if (instrument.Name == "ifm.event_log.batch.commands") Interlocked.Increment(ref _transactions);
                if (instrument.Name == "ifm.event_log.queue.depth")
                {
                    if (value > 0) Interlocked.Add(ref _queuedRequests, value);
                    var depth = Interlocked.Add(ref _depth, value);
                    long seen;
                    do { seen = Interlocked.Read(ref _maximum); if (depth <= seen) break; }
                    while (Interlocked.CompareExchange(ref _maximum, depth, seen) != seen);
                }
            });
            _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
            {
                if (instrument.Name == "ifm.event_log.marker.duration")
                    lock (_markerLock) _markerAwaitMs += value;
            });
            _listener.Start();
        }
        public void Dispose() => _listener.Dispose();
    }

    public sealed record BenchmarkEvent : IEvent, IRequireDurableProjection
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public Guid Id { get; init; } = Guid.NewGuid();
        public long EventId { get; init; }
        public Guid CommandId { get; init; }
        public string AggregateId { get; init; } = "";
        public string EventSource { get; init; } = "EventLogV2Benchmark";
        public DateTime ReceivedOn { get; init; } = FixtureTime;
        public string UserName => "benchmark";
        public string EventName => nameof(BenchmarkEvent);
        public EventType EventType => EventType.DomainEvent;
        public long Value { get; init; }
        public string Payload { get; init; } = "";
        public bool RequiresDurableProjection { get; init; }
        public string ProjectionName { get; init; } = "BenchmarkProjector";
        public EventProjectorStageType InitialStage { get; init; } = EventProjectorStageType.ApplyProjection;
        public DurableProjectionRequirement RequiredProjection =>
            new("BenchmarkActor", ProjectionName, InitialStage);
    }

    public sealed record BenchmarkCommand : ICommand
    {
        public ActorSubject Subject { get; init; } = ActorSubject.Unknown;
        public string CommandName => nameof(BenchmarkCommand);
        public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
        public Guid CommandId { get; init; }
        public string StreamId { get; init; } = "";
        public string EventSource => "EventLogV2Benchmark";
        public int ErrorCode => 1;
        public long Value { get; init; }
        public string Payload { get; init; } = "";
    }
}
