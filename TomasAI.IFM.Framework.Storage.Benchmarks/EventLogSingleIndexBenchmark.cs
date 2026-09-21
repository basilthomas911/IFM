using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// An intentionally stripped, benchmark-only event-log experiment. It does not use or change the
/// production schema or writer and is not a production-compatible event store.
/// </summary>
internal static class EventLogSingleIndexBenchmark
{
    const string AdminVariable = "IFM_EVENTLOG_BENCH_ADMIN_CONNECTION";
    const int ProducerCount = 64;
    const int QueueCapacity = 64;
    const string Limitations = "This does NOT test command audit, financial trigger, global ordering, " +
        "event-id lookup, projector markers/durable replay, outbox, crash acknowledgement, or production cutover.";
    static readonly DateTime FixtureTimestamp = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
    static readonly byte[] Payload = CreatePayload();
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    enum Variant { CurrentLayout, ExactSingleIndex }

    sealed record Options(int Repeats, int SoakSeconds, int BatchEvents, string Output);

    sealed record Sample(
        string Variant,
        int Repetition,
        int RunOrder,
        string Database,
        long Commands,
        long Events,
        double Seconds,
        double CommandsPerSecond,
        double EventsPerSecond,
        double P50AppendAcknowledgementMs,
        double P95AppendAcknowledgementMs,
        double P99AppendAcknowledgementMs,
        double ClientCpuMs,
        long AllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections,
        long WalBytes,
        long FinalTableBytes,
        long FinalIndexBytes,
        long MaximumQueueDepth,
        long CommittedTransactions,
        double ReplayElapsedMs,
        long ReplayEvents,
        bool Verified,
        string[] IndexDefinitions);

    sealed record PipelineResult(double[] Latencies, long[] StreamVersions, double MeasurementSeconds,
        long MaximumQueueDepth, long CommittedTransactions);

    sealed class PendingEvent(int producer, long streamVersion, Guid commandId, long started)
    {
        internal int Producer { get; } = producer;
        internal long StreamVersion { get; } = streamVersion;
        internal Guid CommandId { get; } = commandId;
        internal long Started { get; } = started;
        internal TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal static async Task RunAsync(string[] args)
    {
        var options = Parse(args);
        var rawAdmin = Environment.GetEnvironmentVariable(AdminVariable)
            ?? throw new InvalidOperationException($"Set {AdminVariable} to an isolated loopback PostgreSQL server (not port 5432).");
        var adminBuilder = new NpgsqlConnectionStringBuilder(rawAdmin);
        if (adminBuilder.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            adminBuilder.Port == 5432 || adminBuilder.Database != "postgres")
            throw new InvalidOperationException("Use a dedicated loopback server, non-5432 port, and database postgres.");

        if (Directory.Exists(options.Output) && Directory.EnumerateFileSystemEntries(options.Output).Any())
            throw new InvalidOperationException("Output directory must be new or empty; existing results will not be overwritten.");
        Directory.CreateDirectory(options.Output);

        await using var admin = new NpgsqlConnection(rawAdmin);
        await admin.OpenAsync();
        var databases = Convert.ToInt64(await Scalar(admin,
            "SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres'"));
        if (databases != 0)
            throw new InvalidOperationException("Benchmark server must be empty. Existing databases are never reused or removed.");
        var durability = (string)(await Scalar(admin,
            "SELECT current_setting('fsync') || '/' || current_setting('synchronous_commit') || '/' || current_setting('full_page_writes')"))!;
        if (durability != "on/on/on")
            throw new InvalidOperationException("Durability must be on/on/on.");

        var runId = Guid.NewGuid().ToString("N")[..12];
        var metadata = new
        {
            RunId = runId,
            StartedUtc = DateTime.UtcNow,
            PostgreSql = (string)(await Scalar(admin, "SELECT version()"))!,
            Durability = durability,
            Runtime = RuntimeInformation.FrameworkDescription,
            OS = RuntimeInformation.OSDescription,
            CpuCount = Environment.ProcessorCount,
            ServerGC = GCSettings.IsServerGC,
            options.Repeats,
            options.SoakSeconds,
            options.BatchEvents,
            Producers = ProducerCount,
            QueueCapacity,
            PayloadBytes = Payload.Length,
            PayloadSha256 = Convert.ToHexString(SHA256.HashData(Payload)),
            Writer = "Same benchmark-only bounded Channel pipeline; one consumer; binary COPY; synchronous durable transaction commits.",
            Layouts = new
            {
                CurrentLayout = "Current seven columns, sequence-backed eventversion, and four indexes.",
                ExactSingleIndex = "Six exact stripped columns and only PRIMARY KEY(eventstreamid,streamversion)."
            },
            Limitations
        };
        await File.WriteAllTextAsync(Path.Combine(options.Output, "metadata.json"),
            JsonSerializer.Serialize(metadata, Json));

        var samples = new List<Sample>();
        try
        {
            var variants = new[] { Variant.CurrentLayout, Variant.ExactSingleIndex };
            for (var repeat = 0; repeat < options.Repeats; repeat++)
            for (var order = 0; order < variants.Length; order++)
            {
                var variant = variants[(repeat + order) % variants.Length];
                var database = $"ifm_eventlog_single_{runId}_{repeat + 1}_{(int)variant}";
                Console.WriteLine($"Running {variant} / repeat {repeat + 1} / order {order + 1}");
                var sample = await RunSample(admin, rawAdmin, database, variant, repeat + 1, order + 1, options);
                samples.Add(sample);
                await WriteResults(options.Output, samples);
                Console.WriteLine($"  {sample.EventsPerSecond:F1} events/s; p99 {sample.P99AppendAcknowledgementMs:F2} ms; verification passed");
            }
        }
        finally
        {
            await WriteResults(options.Output, samples);
        }

        Console.WriteLine($"Results: {options.Output}");
    }

    static async Task<Sample> RunSample(NpgsqlConnection admin, string rawAdmin, string database,
        Variant variant, int repetition, int runOrder, Options options)
    {
        // The identifier is generated entirely by this benchmark and never accepts user input.
        await Execute(admin, $"CREATE DATABASE \"{database}\"");
        var success = false;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(rawAdmin)
            {
                Database = database,
                Pooling = true,
                ApplicationName = "IFM exact single-index benchmark"
            };
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await Execute(connection, Ddl(variant));
            await VerifyShape(connection, variant);
            var indexDefinitions = await ReadIndexDefinitions(connection);

            var walBefore = (string)(await Scalar(connection, "SELECT pg_current_wal_insert_lsn()::text"))!;
            var gcBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            var allocatedBefore = GC.GetTotalAllocatedBytes(true);
            using var process = Process.GetCurrentProcess();
            var cpuBefore = process.TotalProcessorTime;
            var pipeline = await RunPipeline(builder.ConnectionString, variant, options.SoakSeconds,
                options.BatchEvents);
            var cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
            var collections = Enumerable.Range(0, 3)
                .Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray();
            var walBytes = Convert.ToInt64(await Scalar(connection,
                "SELECT pg_wal_lsn_diff(pg_current_wal_insert_lsn(), $1::pg_lsn)::bigint", walBefore));

            if (pipeline.MaximumQueueDepth > QueueCapacity)
                throw new InvalidOperationException("Observed queue depth exceeded the bounded channel capacity.");
            var expectedEvents = pipeline.StreamVersions.Sum();
            if (expectedEvents != pipeline.Latencies.LongLength)
                throw new InvalidOperationException("Acknowledgement count does not equal producer state.");
            var replay = await VerifyAndReplay(connection, pipeline.StreamVersions, expectedEvents);
            var tableBytes = Convert.ToInt64(await Scalar(connection, "SELECT pg_table_size('event_log'::regclass)"));
            var indexBytes = Convert.ToInt64(await Scalar(connection, "SELECT pg_indexes_size('event_log'::regclass)"));

            Array.Sort(pipeline.Latencies);
            var seconds = pipeline.MeasurementSeconds;
            var sample = new Sample(variant.ToString(), repetition, runOrder, database,
                expectedEvents, expectedEvents, seconds, expectedEvents / seconds, expectedEvents / seconds,
                Percentile(pipeline.Latencies, .50), Percentile(pipeline.Latencies, .95),
                Percentile(pipeline.Latencies, .99), cpuMs, allocatedBytes,
                collections[0], collections[1], collections[2], walBytes, tableBytes, indexBytes,
                pipeline.MaximumQueueDepth, pipeline.CommittedTransactions,
                replay.ElapsedMs, replay.Events, true, indexDefinitions);
            success = true;
            return sample;
        }
        finally
        {
            // Failed fixtures are deliberately retained for diagnosis; only a fully verified fixture is disposable.
            if (success)
            {
                NpgsqlConnection.ClearAllPools();
                await Execute(admin, $"DROP DATABASE \"{database}\" WITH (FORCE)");
            }
            else
            {
                Console.Error.WriteLine($"Retained failed isolated fixture: {database}");
            }
        }
    }

    static async Task<PipelineResult> RunPipeline(string connectionString, Variant variant,
        int soakSeconds, int batchEvents)
    {
        var channel = Channel.CreateBounded<PendingEvent>(new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        var latencies = new ConcurrentBag<double>();
        var versions = new long[ProducerCount];
        long maximumQueueDepth = 0;
        long transactions = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);

        void ObserveDepth()
        {
            var depth = channel.Reader.Count;
            long seen;
            do
            {
                seen = Interlocked.Read(ref maximumQueueDepth);
                if (depth <= seen) return;
            } while (Interlocked.CompareExchange(ref maximumQueueDepth, depth, seen) != seen);
        }

        var consumer = Task.Run(async () =>
        {
            var batch = new List<PendingEvent>(batchEvents);
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                await Execute(connection, "SET synchronous_commit=on");
                ready.TrySetResult();
                while (await channel.Reader.WaitToReadAsync())
                {
                    batch.Clear();
                    if (!channel.Reader.TryRead(out var first)) continue;
                    batch.Add(first);
                    while (batch.Count < batchEvents && channel.Reader.TryRead(out var next)) batch.Add(next);
                    await CopyAndCommit(connection, variant, batch);
                    Interlocked.Increment(ref transactions);
                    foreach (var pending in batch) pending.Completion.TrySetResult();
                }
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
                channel.Writer.TryComplete(ex);
                foreach (var pending in batch) pending.Completion.TrySetException(ex);
                while (channel.Reader.TryRead(out var pending)) pending.Completion.TrySetException(ex);
                throw;
            }
        });

        await ready.Task;
        var producerTasks = Enumerable.Range(0, ProducerCount).Select(async producer =>
        {
            var started = await start.Task;
            var deadline = started + (long)(soakSeconds * (double)Stopwatch.Frequency);
            long version = 0;
            while (Stopwatch.GetTimestamp() < deadline)
            {
                var pending = new PendingEvent(producer, version + 1, Guid.NewGuid(), Stopwatch.GetTimestamp());
                await channel.Writer.WriteAsync(pending);
                ObserveDepth();
                await pending.Completion.Task;
                latencies.Add(Stopwatch.GetElapsedTime(pending.Started).TotalMilliseconds);
                version++;
            }
            versions[producer] = version;
        }).ToArray();

        var measurementStarted = Stopwatch.GetTimestamp();
        start.TrySetResult(measurementStarted);
        try
        {
            await Task.WhenAll(producerTasks);
            channel.Writer.TryComplete();
            await consumer;
        }
        catch
        {
            channel.Writer.TryComplete();
            try { await consumer; } catch { }
            throw;
        }

        if (channel.Reader.Count != 0)
            throw new InvalidOperationException("Queue depth was not zero after the consumer drained.");
        var measurementSeconds = Stopwatch.GetElapsedTime(measurementStarted).TotalSeconds;
        return new PipelineResult(latencies.ToArray(), versions, measurementSeconds,
            Interlocked.Read(ref maximumQueueDepth), Interlocked.Read(ref transactions));
    }

    static async Task CopyAndCommit(NpgsqlConnection connection, Variant variant,
        IReadOnlyList<PendingEvent> batch)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var copy = variant == Variant.CurrentLayout
            ? "COPY event_log (eventstreamid,eventnameid,streamversion,eventpayload,commandid,eventtimestamp) FROM STDIN (FORMAT BINARY)"
            : "COPY event_log (eventstreamid,streamversion,commandid,eventnameid,eventtimestamp,eventpayload) FROM STDIN (FORMAT BINARY)";
        await using (var importer = await connection.BeginBinaryImportAsync(copy))
        {
            foreach (var pending in batch)
            {
                await importer.StartRowAsync();
                if (variant == Variant.CurrentLayout)
                {
                    await importer.WriteAsync((long)pending.Producer + 1, NpgsqlDbType.Bigint);
                    await importer.WriteAsync(1, NpgsqlDbType.Integer);
                    await importer.WriteAsync(pending.StreamVersion, NpgsqlDbType.Bigint);
                    await importer.WriteAsync(Payload, NpgsqlDbType.Bytea);
                    await importer.WriteAsync(pending.CommandId, NpgsqlDbType.Uuid);
                    await importer.WriteAsync(FixtureTimestamp.ToString("O", CultureInfo.InvariantCulture), NpgsqlDbType.Text);
                }
                else
                {
                    await importer.WriteAsync((long)pending.Producer + 1, NpgsqlDbType.Bigint);
                    await importer.WriteAsync(pending.StreamVersion, NpgsqlDbType.Bigint);
                    await importer.WriteAsync(pending.CommandId, NpgsqlDbType.Uuid);
                    await importer.WriteAsync(1, NpgsqlDbType.Integer);
                    await importer.WriteAsync(FixtureTimestamp, NpgsqlDbType.TimestampTz);
                    await importer.WriteAsync(Payload, NpgsqlDbType.Bytea);
                }
            }
            await importer.CompleteAsync();
        }
        await transaction.CommitAsync();
    }

    static async Task VerifyShape(NpgsqlConnection connection, Variant variant)
    {
        var columns = new List<(string Name, string Type, string Nullable, string? Default)>();
        await using (var command = new NpgsqlCommand("""
            SELECT column_name,data_type,is_nullable,column_default
            FROM information_schema.columns
            WHERE table_schema='public' AND table_name='event_log'
            ORDER BY ordinal_position
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));

        var expected = variant == Variant.CurrentLayout
            ? new[] { ("eventstreamid", "bigint"), ("eventnameid", "integer"), ("eventversion", "bigint"),
                ("streamversion", "bigint"), ("eventpayload", "bytea"), ("commandid", "uuid"),
                ("eventtimestamp", "text") }
            : new[] { ("eventstreamid", "bigint"), ("streamversion", "bigint"), ("commandid", "uuid"),
                ("eventnameid", "integer"), ("eventtimestamp", "timestamp with time zone"),
                ("eventpayload", "bytea") };
        if (columns.Count != expected.Length || columns.Where((column, i) =>
                column.Name != expected[i].Item1 || column.Type != expected[i].Item2 || column.Nullable != "NO").Any())
            throw new InvalidOperationException($"{variant} column shape is not exact.");
        if (variant == Variant.CurrentLayout)
        {
            if (columns[2].Default is null || !columns[2].Default.Contains("nextval", StringComparison.Ordinal))
                throw new InvalidOperationException("CurrentLayout eventversion is not sequence-backed.");
        }
        else if (columns.Any(column => column.Default is not null))
            throw new InvalidOperationException("ExactSingleIndex must not have column defaults.");

        var indexes = await ReadIndexDefinitions(connection);
        if (indexes.Length != (variant == Variant.CurrentLayout ? 4 : 1))
            throw new InvalidOperationException($"Unexpected {variant} index count: {indexes.Length}.");
        var primary = (string)(await Scalar(connection,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='event_log'::regclass AND contype='p'"))!;
        var expectedPrimary = variant == Variant.CurrentLayout
            ? "PRIMARY KEY (eventstreamid, eventnameid, eventversion)"
            : "PRIMARY KEY (eventstreamid, streamversion)";
        if (primary != expectedPrimary)
            throw new InvalidOperationException("Unexpected primary key: " + primary);
        if (variant == Variant.CurrentLayout &&
            (!indexes.Any(x => x.Contains("UNIQUE INDEX ux_event_log_event_version", StringComparison.Ordinal) && x.EndsWith("(eventversion)", StringComparison.Ordinal)) ||
             !indexes.Any(x => x.Contains("INDEX ix_event_log_command_id", StringComparison.Ordinal) && x.EndsWith("(commandid)", StringComparison.Ordinal)) ||
             !indexes.Any(x => x.Contains("UNIQUE INDEX ux_event_log_stream_version_v3", StringComparison.Ordinal) && x.EndsWith("(eventstreamid, streamversion)", StringComparison.Ordinal))))
            throw new InvalidOperationException("CurrentLayout secondary indexes do not match the current schema.");

        var checks = Convert.ToInt32(await Scalar(connection,
            "SELECT count(*) FROM pg_constraint WHERE conrelid='event_log'::regclass AND contype='c'"));
        if (checks != 1) throw new InvalidOperationException("Expected exactly one payload check constraint.");
        var triggers = Convert.ToInt32(await Scalar(connection,
            "SELECT count(*) FROM pg_trigger WHERE tgrelid='event_log'::regclass AND NOT tgisinternal"));
        if (triggers != 0) throw new InvalidOperationException("Benchmark layouts must not have triggers.");
        var sequences = Convert.ToInt32(await Scalar(connection,
            "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind='S' AND n.nspname='public'"));
        if (sequences != (variant == Variant.CurrentLayout ? 1 : 0))
            throw new InvalidOperationException($"Unexpected {variant} sequence count: {sequences}.");
    }

    static async Task<(double ElapsedMs, long Events)> VerifyAndReplay(NpgsqlConnection connection,
        long[] versions, long expectedEvents)
    {
        var rows = Convert.ToInt64(await Scalar(connection, "SELECT count(*) FROM event_log"));
        var pairs = Convert.ToInt64(await Scalar(connection,
            "SELECT count(DISTINCT (eventstreamid,streamversion)) FROM event_log"));
        var payloads = Convert.ToInt64(await Scalar(connection,
            "SELECT count(*) FROM event_log WHERE eventpayload=$1", Payload));
        if (rows != expectedEvents || pairs != expectedEvents || payloads != expectedEvents)
            throw new InvalidOperationException($"Durable verification failed: rows={rows}, pairs={pairs}, payloads={payloads}, expected={expectedEvents}.");

        var replayTimer = Stopwatch.StartNew();
        var replayVersions = new long[ProducerCount];
        long replayEvents = 0;
        await using var command = new NpgsqlCommand(
            "SELECT eventstreamid,streamversion,eventpayload FROM event_log ORDER BY eventstreamid,streamversion", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var stream = reader.GetInt64(0);
            if (stream < 1 || stream > ProducerCount)
                throw new InvalidOperationException("Replay encountered an unknown stream.");
            var index = checked((int)stream - 1);
            var next = replayVersions[index] + 1;
            if (reader.GetInt64(1) != next || !reader.GetFieldValue<byte[]>(2).AsSpan().SequenceEqual(Payload))
                throw new InvalidOperationException("Per-stream replay order or payload mismatch.");
            replayVersions[index] = next;
            replayEvents++;
        }
        replayTimer.Stop();
        if (replayEvents != expectedEvents || !replayVersions.SequenceEqual(versions))
            throw new InvalidOperationException("Replay did not rebuild producer stream state.");
        return (replayTimer.Elapsed.TotalMilliseconds, replayEvents);
    }

    static async Task<string[]> ReadIndexDefinitions(NpgsqlConnection connection)
    {
        var result = new List<string>();
        await using var command = new NpgsqlCommand("""
            SELECT indexdef FROM pg_indexes
            WHERE schemaname='public' AND tablename='event_log'
            ORDER BY indexname
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    static string Ddl(Variant variant) => variant == Variant.CurrentLayout ? """
        CREATE SEQUENCE public.event_log_eventversion_seq;
        CREATE TABLE public.event_log (
            eventstreamid bigint NOT NULL,
            eventnameid integer NOT NULL,
            eventversion bigint DEFAULT nextval('public.event_log_eventversion_seq'::regclass) NOT NULL,
            streamversion bigint NOT NULL,
            eventpayload bytea NOT NULL CHECK (octet_length(eventpayload) > 0),
            commandid uuid NOT NULL,
            eventtimestamp text NOT NULL,
            CONSTRAINT event_log_pkey PRIMARY KEY (eventstreamid,eventnameid,eventversion)
        );
        CREATE INDEX ix_event_log_command_id ON public.event_log (commandid);
        CREATE UNIQUE INDEX ux_event_log_event_version ON public.event_log (eventversion);
        CREATE UNIQUE INDEX ux_event_log_stream_version_v3 ON public.event_log (eventstreamid,streamversion);
        """ : """
        CREATE TABLE public.event_log (
            eventstreamid bigint NOT NULL,
            streamversion bigint NOT NULL,
            commandid uuid NOT NULL,
            eventnameid integer NOT NULL,
            eventtimestamp timestamptz NOT NULL,
            eventpayload bytea NOT NULL CHECK (octet_length(eventpayload) > 0),
            CONSTRAINT event_log_pkey PRIMARY KEY (eventstreamid,streamversion)
        );
        """;

    static async Task WriteResults(string output, IReadOnlyList<Sample> samples)
    {
        await File.WriteAllTextAsync(Path.Combine(output, "samples.json"), JsonSerializer.Serialize(samples, Json));
        var summary = new StringBuilder("# Exact stripped event-log single-index experiment\n\n");
        summary.AppendLine("Isolated benchmark-only comparison. Every reported sample passed durable row, unique stream/version, exact payload, ordered replay, and drained-queue verification.\n");
        summary.AppendLine("| Variant | Samples | Median commands/s | Median events/s | Median p50 ms | Median p95 ms | Median p99 ms | Median CPU ms | Median allocated bytes | Median Gen0/1/2 | Median WAL bytes | Median table bytes | Median index bytes | Median max queue | Median transactions | Median replay ms |");
        summary.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var group in samples.GroupBy(sample => sample.Variant))
        {
            var medianCollections = FormattableString.Invariant(
                $"{Median(group.Select(x => (double)x.Gen0Collections)):F1}/{Median(group.Select(x => (double)x.Gen1Collections)):F1}/{Median(group.Select(x => (double)x.Gen2Collections)):F1}");
            summary.AppendLine(FormattableString.Invariant(
                $"| {group.Key} | {group.Count()} | {Median(group.Select(x => x.CommandsPerSecond)):F1} | {Median(group.Select(x => x.EventsPerSecond)):F1} | {Median(group.Select(x => x.P50AppendAcknowledgementMs)):F2} | {Median(group.Select(x => x.P95AppendAcknowledgementMs)):F2} | {Median(group.Select(x => x.P99AppendAcknowledgementMs)):F2} | {Median(group.Select(x => x.ClientCpuMs)):F0} | {Median(group.Select(x => (double)x.AllocatedBytes)):F0} | {medianCollections} | {Median(group.Select(x => (double)x.WalBytes)):F0} | {Median(group.Select(x => (double)x.FinalTableBytes)):F0} | {Median(group.Select(x => (double)x.FinalIndexBytes)):F0} | {Median(group.Select(x => (double)x.MaximumQueueDepth)):F0} | {Median(group.Select(x => (double)x.CommittedTransactions)):F0} | {Median(group.Select(x => x.ReplayElapsedMs)):F2} |"));
        }
        summary.AppendLine("\n## Paired throughput deltas\n");
        var current = samples.Where(x => x.Variant == nameof(Variant.CurrentLayout)).ToDictionary(x => x.Repetition);
        var deltas = samples.Where(x => x.Variant == nameof(Variant.ExactSingleIndex) && current.ContainsKey(x.Repetition))
            .Select(x => (Repetition: x.Repetition,
                Delta: 100 * (x.EventsPerSecond / current[x.Repetition].EventsPerSecond - 1))).ToArray();
        foreach (var delta in deltas)
            summary.AppendLine(FormattableString.Invariant($"- Repeat {delta.Repetition}: ExactSingleIndex vs CurrentLayout {delta.Delta:+0.0;-0.0;0.0}% events/s."));
        if (deltas.Length > 0)
            summary.AppendLine(FormattableString.Invariant($"- Median paired throughput delta: {Median(deltas.Select(x => x.Delta)):+0.0;-0.0;0.0}%."));
        summary.AppendLine("\n## Scope\n");
        summary.AppendLine(Limitations);
        summary.AppendLine("Production code and production schema are unchanged. The stripped candidate intentionally removes capabilities and is not cutover evidence.");
        await File.WriteAllTextAsync(Path.Combine(output, "summary.md"), summary.ToString());
    }

    static Options Parse(string[] args)
    {
        var allowed = new[] { "--repeats=", "--soak-seconds=", "--batch-events=", "--output=" };
        if (args.Any(arg => !allowed.Any(key => arg.StartsWith(key, StringComparison.Ordinal))))
            throw new ArgumentException("Options: --repeats=N --soak-seconds=N --batch-events=N --output=PATH");
        var repeats = Number(args, "--repeats=", 5, 2, 30);
        var soakSeconds = Number(args, "--soak-seconds=", 10, 10, 600);
        var batchEvents = Number(args, "--batch-events=", 256, 1, 4096);
        var suppliedOutput = Single(args, "--output=");
        var runDirectory = Guid.NewGuid().ToString("N")[..12];
        var output = Path.GetFullPath(suppliedOutput ??
            Path.Combine("BenchmarkDotNet.Artifacts", "event-log-single-index", runDirectory));
        if (string.IsNullOrWhiteSpace(suppliedOutput) && suppliedOutput is not null)
            throw new ArgumentException("--output must not be empty.");
        return new Options(repeats, soakSeconds, batchEvents, output);
    }

    static int Number(string[] args, string key, int fallback, int minimum, int maximum)
    {
        var raw = Single(args, key);
        if (raw is null) return fallback;
        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(key, $"{key} must be from {minimum} through {maximum}.");
        return value;
    }

    static string? Single(string[] args, string key)
    {
        var supplied = args.Where(arg => arg.StartsWith(key, StringComparison.Ordinal)).ToArray();
        if (supplied.Length > 1) throw new ArgumentException($"Duplicate {key}");
        return supplied.Length == 0 ? null : supplied[0][key.Length..];
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

    static double Percentile(double[] sorted, double fraction) =>
        sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * fraction) - 1, 0, sorted.Length - 1)];

    static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length == 0 ? 0 : sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    static byte[] CreatePayload()
    {
        var payload = new byte[1024];
        new Random(20260920).NextBytes(payload);
        return payload;
    }
}
