using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Destructive fault injection only against newly owned, isolated benchmark databases.</summary>
internal static class EventLogMarkerQualification
{
    const long GateKey = 19760919;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    sealed record Result(string Variant, int Repetition, string Scenario, string Database,
        bool Passed, double Seconds, string Detail);

    internal static async Task RunAsync(string[] args)
    {
        if (args.Any(a => a != "--process-restart" && a != "--schema-comparison" && !a.StartsWith("--output=", StringComparison.Ordinal)))
            throw new ArgumentException("Options: --output=PATH --process-restart --schema-comparison");
        var processRestart = args.Contains("--process-restart");
        var schemaComparison = args.Contains("--schema-comparison");
        var raw = Environment.GetEnvironmentVariable(EventLogV2Benchmark.AdminVariable)
            ?? throw new InvalidOperationException("An isolated benchmark admin connection is required.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(raw);
        if (adminBuilder.Host is not ("localhost" or "127.0.0.1" or "::1") ||
            adminBuilder.Port == 5432 || adminBuilder.Database != "postgres")
            throw new InvalidOperationException("Requires a dedicated loopback, non-5432 PostgreSQL server.");
        // Administrative connections survive deliberate server faults only by opening a fresh socket.
        adminBuilder.Pooling = false;
        raw = adminBuilder.ConnectionString;
        var run = Guid.NewGuid().ToString("N")[..12];
        var output = Path.GetFullPath(args.FirstOrDefault(a => a.StartsWith("--output="))?[9..] ??
            Path.Combine("BenchmarkDotNet.Artifacts", "event-log-v2", "qualification-" + run));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException("Output directory must be new or empty.");
        await using var admin = new NpgsqlConnection(raw);
        await admin.OpenAsync();
        Check(Convert.ToInt64(await Sql(admin,
            "SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres'")) == 0,
            "Server is not empty; existing databases will not be touched.");
        Check((string)(await Sql(admin,
            "SELECT current_setting('fsync')||'/'||current_setting('synchronous_commit')||'/'||current_setting('full_page_writes')"))!
            == "on/on/on", "Durability must remain on/on/on.");
        Directory.CreateDirectory(output);
        if (processRestart) await EventLogProcessQualification.ValidateContainer(raw);
        var results = new List<Result>();
        await File.WriteAllTextAsync(Path.Combine(output, "metadata.json"), JsonSerializer.Serialize(new
        {
            Run = run, StartedUtc = DateTime.UtcNow, PostgreSql = await Sql(admin, "SELECT version()"),
            Repetitions = 3, Durability = "on/on/on",
            SchemaComparison = schemaComparison,
            Scope = processRestart ? "Independent writer processes, abrupt writer death, graceful database restart and SIGKILL server recovery; not physical power-loss testing."
                : "Independent writer connections; checkpoint race; backend termination; cancellation; lost COMMIT acknowledgment. Not OS/process/power-loss qualification.",
            ProductionChanged = false,
            HarnessSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogMarkerQualification.cs"))),
            ProcessHarnessSha256 = processRestart ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(
                "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogProcessQualification.cs"))) : null
        }, Json));
        foreach (var candidate in new[] { false, true })
        {
            var batch = schemaComparison || candidate;
            var variant = schemaComparison ? (candidate ? "threeindex" : "fourindex") : batch ? "batched" : "baseline";
            var database = $"ifm_eventlog_bench_{run}_{variant}";
            var builder = new NpgsqlConnectionStringBuilder(raw)
                { Database = database, Pooling = false, ApplicationName = "MarkerQualification" };
            var direct = builder.ConnectionString;
            builder.Username = ""; builder.Password = "";
            var provider = builder.ConnectionString;
            var layout = EventLogSqlLayout.ForBenchmark(provider, true, batch);
            await Sql(admin, $"CREATE DATABASE \"{database}\"");
            var success = false;
            try
            {
                var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
                    provider, "System.Data.Postgres");
                await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
                await using var db = new NpgsqlConnection(direct);
                await db.OpenAsync();
                await Sql(db, PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01);
                await Sql(db, "ALTER TABLE event_log RENAME TO event_log_v2");
                if (schemaComparison && candidate)
                    await Sql(db, """
                        ALTER TABLE event_log_v2 DROP CONSTRAINT event_log_pkey;
                        ALTER TABLE event_log_v2 ADD CONSTRAINT event_log_v2_pkey
                            PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
                        """);
                Check(Convert.ToInt64(await Sql(db,
                    "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='event_log_v2'")) == (schemaComparison && candidate ? 3 : 4),
                    "Unexpected event-log index count.");
                Check((string)(await Sql(db, """
                    SELECT pg_get_constraintdef(oid) FROM pg_constraint
                    WHERE conrelid='event_log_v2'::regclass AND contype='p'
                    """))! == (schemaComparison && candidate
                        ? "PRIMARY KEY (eventstreamid, streamversion)"
                        : "PRIMARY KEY (eventstreamid, eventnameid, eventversion)"),
                    "Unexpected primary key.");
                Check(Convert.ToInt64(await Sql(db, """
                    SELECT count(*) FROM pg_constraint WHERE confrelid='event_log_v2'::regclass AND contype='f'
                    """)) >= 5, "Financial identity foreign keys must remain.");
                var eventNameId = Convert.ToInt32(await Sql(db,
                    "INSERT INTO event_name_id(eventname,eventtypename) VALUES($1,$2) RETURNING eventnameid",
                    nameof(EventLogV2Benchmark.BenchmarkEvent),
                    typeof(EventLogV2Benchmark.BenchmarkEvent).AssemblyQualifiedName!));
                for (var repetition = 1; repetition <= 3; repetition++)
                foreach (var scenario in processRestart
                    ? new[] { "process-version-race", "process-duplicate", "process-death", "graceful-restart", "server-crash" }
                    : new[] { "competing-stream-version", "duplicate-command", "checkpoint-advance",
                        "backend-termination", "cancel-after-admission", "lost-commit-ack" })
                {
                    var timer = Stopwatch.StartNew();
                    try
                    {
                        if (processRestart)
                            await EventLogProcessQualification.RunCase(db, direct, provider, batch, eventNameId,
                                scenario, repetition, output);
                        else await RunCase(db, direct, provider, layout, eventNameId, scenario, repetition);
                        results.Add(new(variant, repetition, scenario, database, true, timer.Elapsed.TotalSeconds,
                            "Durable events, markers, command audit, stream counter and retry invariants passed."));
                        Console.WriteLine($"PASS {variant} {repetition} {scenario}");
                    }
                    catch (Exception ex)
                    {
                        results.Add(new(variant, repetition, scenario, database, false, timer.Elapsed.TotalSeconds, ex.ToString()));
                        throw;
                    }
                    finally
                    {
                        await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, Json));
                    }
                }
                success = true;
            }
            finally
            {
                if (success)
                {
                    if (processRestart) { await admin.CloseAsync(); await admin.OpenAsync(); }
                    await Sql(admin, $"DROP DATABASE \"{database}\" WITH (FORCE)");
                }
                else Console.Error.WriteLine($"Retained failed isolated fixture: {database}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "summary.md"),
            "# Marker fault/concurrency qualification\n\n" +
            $"{results.Count}/{results.Count} cases passed; 3 repetitions, {(processRestart ? 5 : 6)} scenarios.\n\n" +
            (schemaComparison
                ? "Both writers batch markers; four-index control versus three-index stream primary key. "
                : "Baseline/batched writers both retain four indexes. ") +
            "Command auditing, financial fence and durable markers remain. " +
            "Every successful fixture was dropped. No production configuration or schema changed.\n\n" +
            (processRestart ? "These are correctness checks using separate OS processes and whole-server restarts, not throughput or physical power-loss tests.\n"
                : "These are correctness checks, not throughput samples. Independent connections are not independent OS processes. " +
                  "Backend termination is not database-server/power-loss testing. Checkpoint tests advance only through already committed events.\n"));
        Console.WriteLine($"Qualification complete: {output}");
    }

    static async Task RunCase(NpgsqlConnection db, string direct, string provider, EventLogSqlLayout layout,
        int eventNameId, string scenario, int repetition)
    {
        var stream = $"Qualification.{scenario}.{repetition}";
        var streamId = Convert.ToInt64(await Sql(db,
            "INSERT INTO event_stream_id(eventstream) VALUES($1) RETURNING eventstreamid", stream));
        var request = Request(stream, streamId, eventNameId, 0);
        if (scenario is "competing-stream-version" or "duplicate-command")
        {
            await using var first = Writer(provider, layout);
            await using var second = Writer(provider, layout);
            var other = scenario == "duplicate-command" ? request : Request(stream, streamId, eventNameId, 0);
            // Hold the stream row so independent writer transactions overlap deterministically.
            await using var blocker = new NpgsqlConnection(direct);
            await blocker.OpenAsync();
            await using var transaction = await blocker.BeginTransactionAsync();
            await Sql(blocker, "SELECT eventstreamid FROM event_stream_id WHERE eventstreamid=$1 FOR UPDATE", streamId);
            var one = Capture(() => first.AppendAsync(request).AsTask());
            var two = Capture(() => second.AppendAsync(other).AsTask());
            try
            {
                await Until(async () => Convert.ToInt64(await Sql(db,
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'")) >= 2);
            }
            finally { await transaction.RollbackAsync(); }
            var errors = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(10));
            Check(errors.Count(e => e is null) == 1, "Exactly one competing command must commit.");
            var rejected = errors.Single(e => e is not null);
            Check(scenario == "duplicate-command" ? rejected is CommandAuditDuplicateException : rejected is ConcurrencyException,
                $"Incorrect conflict classification: {rejected}");
            await Verify(db, request, 8, 1);
            var winner = errors[0] is null ? request : other;
            await RetryDuplicate(provider, layout, winner);
            await Verify(db, request, 8, 1);
            return;
        }
        if (scenario == "lost-commit-ack")
        {
            await using var proxy = new CommitAckDropProxy(new NpgsqlConnectionStringBuilder(direct).Port);
            var routed = new NpgsqlConnectionStringBuilder(provider)
                { Host = "127.0.0.1", Port = proxy.Port, SslMode = SslMode.Disable, Pooling = false };
            routed["GSS Encryption Mode"] = "Disable";
            await using (var writer = Writer(routed.ConnectionString, layout))
            {
                var error = await Capture(() => writer.AppendAsync(request).AsTask());
                await proxy.Dropped.WaitAsync(TimeSpan.FromSeconds(5));
                Check(error is EventLogCommitOutcomeUnknownException, $"Expected unknown commit outcome, got {error}");
            }
            await Verify(db, request, 8, 1); // Authoritative read proves the server committed.
            await RetryDuplicate(provider, layout, request);
            await Verify(db, request, 8, 1);
            return;
        }
        var seedCount = 0;
        if (scenario == "checkpoint-advance")
        {
            await using var seed = Writer(provider, layout);
            await seed.AppendAsync(request);
            seedCount = 8;
            await Sql(db, """
                INSERT INTO event_projector_stream_checkpoint(projectorname,eventstreamid,lastappliedstreamversion,lastappliedeventid)
                VALUES ('BenchmarkProjector',$1,0,0)
                """, streamId);
            request = Request(stream, streamId, eventNameId, 8);
        }
        // This trigger exists only in the owned disposable fixture, never in the application schema.
        await Sql(db, $"""
            CREATE OR REPLACE FUNCTION qualification_marker_gate() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock({GateKey}); RETURN NEW; END $$;
            CREATE TRIGGER qualification_marker_gate BEFORE INSERT ON event_projector_state
            FOR EACH ROW EXECUTE FUNCTION qualification_marker_gate()
            """);
        await using var gate = new NpgsqlConnection(direct);
        await gate.OpenAsync();
        await Sql(gate, $"SELECT pg_advisory_lock({GateKey})");
        var released = false;
        using var cancel = new CancellationTokenSource();
        var tag = "QualificationFault-" + Guid.NewGuid().ToString("N");
        var tagged = new NpgsqlConnectionStringBuilder(provider) { ApplicationName = tag };
        await using var appender = Writer(tagged.ConnectionString, layout);
        var pending = Capture(() => appender.AppendAsync(request, cancel.Token).AsTask());
        try
        {
            var pid = 0;
            await Until(async () =>
            {
                pid = Convert.ToInt32(await Sql(db, """
                    SELECT coalesce((SELECT pid FROM pg_stat_activity
                    WHERE datname=current_database() AND application_name=$1
                    AND wait_event_type='Lock' AND wait_event='advisory'),0)
                    """, tag));
                return pid != 0;
            });
            if (scenario == "backend-termination")
                Check((bool)(await Sql(db, """
                    SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                    WHERE pid=$1 AND datname=current_database() AND application_name=$2
                    """, pid, tag))!, "Owned backend was not terminated.");
            else if (scenario == "checkpoint-advance")
                await Sql(db, """
                    UPDATE event_projector_stream_checkpoint SET lastappliedstreamversion=8,
                    lastappliedeventid=(SELECT max(eventversion) FROM event_log_v2 WHERE eventstreamid=$1),
                    revision=revision+1 WHERE eventstreamid=$1
                    """, streamId);
            else cancel.Cancel();
            await Sql(gate, $"SELECT pg_advisory_unlock({GateKey})");
            released = true;
            var error = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (scenario == "backend-termination")
            {
                Check(error is NpgsqlException, $"Expected confirmed precommit connection failure, got {error}");
                await Verify(db, request, 0, 0);
                await using var retry = Writer(provider, layout);
                await retry.AppendAsync(request);
            }
            else Check(error is null, $"Admitted append must complete durably: {error}");
            await Verify(db, request, seedCount + 8, seedCount == 0 ? 1 : 2);
            await RetryDuplicate(provider, layout, request);
            await Verify(db, request, seedCount + 8, seedCount == 0 ? 1 : 2);
        }
        finally
        {
            if (!released) await Sql(gate, $"SELECT pg_advisory_unlock({GateKey})");
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
            await Sql(db, "DROP TRIGGER qualification_marker_gate ON event_projector_state; DROP FUNCTION qualification_marker_gate()");
        }
    }

    static BinaryCopyEventLogAppender Writer(string connection, EventLogSqlLayout layout) =>
        new(connection, true, new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy }, layout);

    internal static EventLogAppendRequest Request(string stream, long streamId, int eventNameId, long expected, Guid? suppliedId = null)
    {
        var commandId = suppliedId ?? Guid.NewGuid();
        var command = new EventLogV2Benchmark.BenchmarkCommand { CommandId = commandId, StreamId = stream, Value = expected + 8 };
        return new(stream, streamId, commandId,
            Enumerable.Range(1, 8).Select(i => new EventLogAppendEntry(eventNameId,
                new EventLogV2Benchmark.BenchmarkEvent { CommandId = commandId, AggregateId = stream,
                    Value = expected + i, RequiresDurableProjection = true, Payload = "qualification" })).ToArray(),
            expected, DateTime.UtcNow, CommandAuditEnvelope.Create(command, new CommandAuditMessagePackCodec()));
    }

    internal static async Task Verify(NpgsqlConnection db, EventLogAppendRequest request, int events, int commands)
    {
        Check(Convert.ToInt64(await Sql(db, "SELECT currentversion FROM event_stream_id WHERE eventstreamid=$1",
            request.EventStreamId)) == events, "Stream counter changed incorrectly.");
        Check(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM event_log_v2 WHERE eventstreamid=$1",
            request.EventStreamId)) == events, "Incorrect durable event count.");
        Check(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM event_projector_state WHERE eventstreamid=$1",
            request.EventStreamId)) == events, "Unexpected extra durable markers.");
        Check(Convert.ToInt64(await Sql(db, """
            SELECT count(*) FROM event_projector_state WHERE eventstreamid=$1
            AND outcome='Processing' AND stage='ApplyProjection' AND projectorname='BenchmarkProjector'
            """, request.EventStreamId)) == events, "Missing or incorrectly covered durable markers.");
        Check(Convert.ToInt64(await Sql(db, """
            SELECT count(*) FROM event_log_v2 e JOIN event_projector_state p ON p.eventid=e.eventversion
            WHERE e.eventstreamid=$1 AND p.streamversion=e.streamversion AND p.eventstreamid=e.eventstreamid
            """, request.EventStreamId)) == events, "Marker/event identities diverged.");
        Check(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM command_log WHERE streamid=$1",
            request.EventStream)) == commands, "Audit rows are not atomic with events.");
        Check(Convert.ToInt64(await Sql(db, """
            SELECT count(*) FROM (
            SELECT streamversion,row_number() OVER(ORDER BY streamversion) AS expected
            FROM event_log_v2 WHERE eventstreamid=$1) s WHERE streamversion<>expected
            """, request.EventStreamId)) == 0, "Stream versions are not contiguous.");
        var codec = new EventLogMessagePackCodec(true);
        await using var read = new NpgsqlCommand("""
            SELECT e.streamversion,e.eventversion,e.eventpayload,e.commandid,c.commandid
            FROM event_log_v2 e LEFT JOIN command_log c ON c.commandid=e.commandid
            WHERE e.eventstreamid=$1 ORDER BY e.streamversion
            """, db);
        read.Parameters.AddWithValue(request.EventStreamId);
        await using var reader = await read.ExecuteReaderAsync();
        var replayed = 0;
        while (await reader.ReadAsync())
        {
            var value = (EventLogV2Benchmark.BenchmarkEvent)codec.Deserialize(
                typeof(EventLogV2Benchmark.BenchmarkEvent).AssemblyQualifiedName!, reader.GetInt64(1),
                reader.GetFieldValue<byte[]>(2));
            Check(value.Value == ++replayed && reader.GetInt64(0) == replayed &&
                value.Payload == "qualification" && value.AggregateId == request.EventStream &&
                value.CommandId == reader.GetGuid(3) && !reader.IsDBNull(4),
                "Payload replay or event-to-command audit linkage failed.");
        }
        Check(replayed == events, "Replay count diverged.");
    }

    static async Task RetryDuplicate(string provider, EventLogSqlLayout layout, EventLogAppendRequest request)
    {
        await using var retry = Writer(provider, layout);
        var error = await Capture(() => retry.AppendAsync(request).AsTask());
        Check(error is CommandAuditDuplicateException, $"Restarted writer failed to deduplicate: {error}");
    }
    static async Task<Exception?> Capture(Func<Task> action)
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(15)); return null; }
        catch (Exception ex) { return ex; }
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static async Task Until(Func<Task<bool>> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!await condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(2)) throw new TimeoutException("Fault barrier was not observed.");
            await Task.Delay(10);
        }
    }
    internal static async Task<object?> Sql(NpgsqlConnection connection, string sql, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 10 };
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync();
    }

    // SSL/GSS must be disabled: inspect framed backend protocol messages, never application payload bytes.
    // Drop only the server's CommandComplete(COMMIT), after commit, before the client can observe success.
    sealed class CommitAckDropProxy : IAsyncDisposable
    {
        readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        readonly CancellationTokenSource _stop = new();
        readonly TaskCompletionSource _dropped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly Task _run;
        public int Port { get; }
        public Task Dropped => _dropped.Task;
        public CommitAckDropProxy(int targetPort)
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _run = Run(targetPort);
        }
        async Task Run(int targetPort)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                using var server = new TcpClient();
                await server.ConnectAsync(IPAddress.Loopback, targetPort, _stop.Token);
                var incoming = client.GetStream();
                var outgoing = server.GetStream();
                var forward = incoming.CopyToAsync(outgoing, _stop.Token);
                try
                {
                    var header = new byte[5];
                    while (true)
                    {
                        await outgoing.ReadExactlyAsync(header, _stop.Token);
                        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1)) - 4;
                        Check(length is >= 0 and <= 67108864, "Invalid backend protocol frame.");
                        var body = new byte[length];
                        await outgoing.ReadExactlyAsync(body, _stop.Token);
                        if (header[0] == (byte)'C' && Encoding.ASCII.GetString(body) == "COMMIT\0")
                        {
                            client.Close();
                            server.Close();
                            _dropped.TrySetResult();
                            break;
                        }
                        await incoming.WriteAsync(header, _stop.Token);
                        await incoming.WriteAsync(body, _stop.Token);
                    }
                }
                finally
                {
                    client.Close(); server.Close();
                    try { await forward; } catch (IOException) { } catch (OperationCanceledException) { }
                    catch (ObjectDisposedException) { }
                }
            }
            catch (Exception ex) { _dropped.TrySetException(ex); }
        }
        public async ValueTask DisposeAsync()
        {
            _stop.Cancel(); _listener.Stop();
            await _run.WaitAsync(TimeSpan.FromSeconds(5));
            _stop.Dispose();
        }
    }
}
