using System.Text.Json;
using System.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Benchmarks.EventLogMarkerQualification;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>Explicit disposable-database rehearsal only; not registered in application startup.</summary>
internal static class EventLogIndexMigrationQualification
{
    static readonly List<object> Timings = [];
    // Keep the existing index name: unchanged V3 bootstrap then recognizes the promoted index.
    internal const string Forward = """
        DO $migration$
        DECLARE shape text;
        BEGIN
            SELECT pg_get_constraintdef(oid) INTO shape FROM pg_constraint
                WHERE conrelid='public.event_log'::regclass AND contype='p';
            IF shape = 'PRIMARY KEY (eventstreamid, eventnameid, eventversion)' THEN
                ALTER TABLE public.event_log DROP CONSTRAINT event_log_pkey;
                ALTER TABLE public.event_log ADD CONSTRAINT ux_event_log_stream_version_v3
                    PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
            ELSIF shape IS DISTINCT FROM 'PRIMARY KEY (eventstreamid, streamversion)' THEN
                RAISE EXCEPTION 'Unsupported event_log primary key: %', shape;
            END IF;
        END $migration$;
        """;
    internal const string Reverse = """
        DO $migration$
        DECLARE shape text;
        BEGIN
            SELECT pg_get_constraintdef(oid) INTO shape FROM pg_constraint
                WHERE conrelid='public.event_log'::regclass AND contype='p';
            IF shape = 'PRIMARY KEY (eventstreamid, streamversion)' THEN
                ALTER TABLE public.event_log DROP CONSTRAINT ux_event_log_stream_version_v3;
                ALTER TABLE public.event_log ADD CONSTRAINT event_log_pkey
                    PRIMARY KEY (eventstreamid, eventnameid, eventversion);
                CREATE UNIQUE INDEX ux_event_log_stream_version_v3
                    ON public.event_log(eventstreamid,streamversion);
            ELSIF shape IS DISTINCT FROM 'PRIMARY KEY (eventstreamid, eventnameid, eventversion)' THEN
                RAISE EXCEPTION 'Unsupported event_log primary key: %', shape;
            END IF;
        END $migration$;
        """;

    internal static async Task RunAsync(string[] args)
    {
        if (args.Any(a => !a.StartsWith("--output=", StringComparison.Ordinal) && !a.StartsWith("--seed-events=", StringComparison.Ordinal)))
            throw new ArgumentException("Options: --output=PATH --seed-events=N");
        var seedEvents = int.Parse(args.FirstOrDefault(a => a.StartsWith("--seed-events="))?[14..] ?? "256");
        if (seedEvents < 256 || seedEvents > 1048576 || seedEvents % 256 != 0)
            throw new ArgumentException("Seed events must be a multiple of 256 between 256 and 1048576.");
        Timings.Clear();
        var raw = Environment.GetEnvironmentVariable(EventLogV2Benchmark.AdminVariable)
            ?? throw new InvalidOperationException("Isolated benchmark admin connection required.");
        await EventLogProcessQualification.ValidateContainer(raw);
        var adminConfig = new NpgsqlConnectionStringBuilder(raw) { Pooling = false };
        await using var admin = new NpgsqlConnection(adminConfig.ConnectionString);
        await admin.OpenAsync();
        Require((string)(await Sql(admin, "SELECT current_setting('fsync')||'/'||current_setting('synchronous_commit')||'/'||current_setting('full_page_writes')"))!
            == "on/on/on", "Durability must remain enabled.");
        Require(Convert.ToInt64(await Sql(admin, "SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname <> 'postgres'")) == 0,
            "Benchmark server must be empty.");
        var output = Path.GetFullPath(args.FirstOrDefault(a => a.StartsWith("--output="))?[9..] ??
            Path.Combine("BenchmarkDotNet.Artifacts", "event-log-v2", "migration-" + Guid.NewGuid().ToString("N")));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException("Output must be new or empty.");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "forward.sql"), Forward);
        await File.WriteAllTextAsync(Path.Combine(output, "reverse.sql"), Reverse);
        var results = new List<string>();
        var run = Guid.NewGuid().ToString("N")[..12];
        foreach (var populated in new[] { false, true })
        {
            var database = $"ifm_eventlog_bench_{run}_{(populated ? "populated" : "empty")}";
            var config = new NpgsqlConnectionStringBuilder(raw) { Database = database, Pooling = false };
            var direct = config.ConnectionString;
            config.Username = ""; config.Password = "";
            var provider = config.ConnectionString;
            var layout = EventLogSqlLayout.ForBenchmark(provider, batchProjectionMarkers: true);
            await Sql(admin, $"CREATE DATABASE {database}");
            var success = false;
            try
            {
                var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
                    provider, "System.Data.Postgres");
                var schema = new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance);
                await schema.CreateAllAsync();
                await using var db = new NpgsqlConnection(direct);
                await db.OpenAsync();
                await Sql(db, PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01);
                var streamId = Convert.ToInt64(await Sql(db,
                    "INSERT INTO event_stream_id(eventstream) VALUES('MigrationProbe') RETURNING eventstreamid"));
                var eventNameId = Convert.ToInt32(await Sql(db,
                    "INSERT INTO event_name_id(eventname,eventtypename) VALUES($1,$2) RETURNING eventnameid",
                    nameof(EventLogV2Benchmark.BenchmarkEvent), typeof(EventLogV2Benchmark.BenchmarkEvent).AssemblyQualifiedName!));
                long expected = 0;
                async Task Append(int count = 8)
                {
                    await using var writer = new BinaryCopyEventLogAppender(provider, true,
                        new EventLogPersistenceOptions { WriteMode = EventLogWriteMode.BinaryCopy }, layout);
                    var request = Request("MigrationProbe", streamId, eventNameId, expected);
                    var command = new EventLogV2Benchmark.BenchmarkCommand { CommandId = request.CommandId, StreamId = "MigrationProbe", Value = expected + count };
                    request = request with {
                        Events = Enumerable.Range(1, count).Select(i => new EventLogAppendEntry(eventNameId,
                            new EventLogV2Benchmark.BenchmarkEvent { CommandId = request.CommandId, AggregateId = "MigrationProbe",
                                Value = expected + i, RequiresDurableProjection = true, Payload = new string('x', 1024) })).ToArray(),
                        CommandAudit = CommandAuditEnvelope.Create(command, new CommandAuditMessagePackCodec())
                    };
                    await writer.AppendAsync(request);
                    expected += count;
                    Require(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM event_log")) == expected, "Append count");
                    Require(Convert.ToInt64(await Sql(db, "SELECT currentversion FROM event_stream_id WHERE eventstreamid=$1", streamId)) == expected, "Stream version");
                }
                if (populated) for (var i = 0; i < seedEvents / 256; i++) await Append(256);
                Console.WriteLine($"Seed: {expected} events; {await Sql(db, "SELECT pg_total_relation_size('event_log')")} event/index bytes.");
                async Task Check(string stage, bool candidate, string before)
                {
                    Require(before == await Fingerprint(db), "Data changed during " + stage);
                    Require(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='event_log'")) == (candidate ? 3 : 4), "Index count " + stage);
                    Require((string)(await Sql(db, "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='event_log'::regclass AND contype='p'"))!
                        == (candidate ? "PRIMARY KEY (eventstreamid, streamversion)" : "PRIMARY KEY (eventstreamid, eventnameid, eventversion)"), "PK " + stage);
                    Require(Convert.ToInt64(await Sql(db, "SELECT count(*) FROM pg_constraint WHERE confrelid='event_log'::regclass AND contype='f'")) >= 5, "Identity FKs");
                    results.Add($"{(populated ? "populated" : "empty")}: {stage} passed");
                    Console.WriteLine(results[^1]);
                    await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                }
                var before = await Fingerprint(db);
                await using (var blocker = new NpgsqlConnection(direct))
                {
                    await blocker.OpenAsync();
                    await using var held = await blocker.BeginTransactionAsync();
                    await Sql(blocker, "LOCK TABLE public.event_log IN ACCESS SHARE MODE");
                    try
                    {
                        await Migrate(db, Forward);
                        throw new InvalidOperationException("Migration unexpectedly bypassed blocking reader.");
                    }
                    catch (PostgresException ex) when (ex.SqlState == "55P03") { }
                    await held.RollbackAsync();
                }
                await Check("blocked migration fails within lock timeout", false, before);
                try
                {
                    await Migrate(db, Forward + "\nSELECT 1/0;");
                    throw new InvalidOperationException("Expected injected SQL failure.");
                }
                catch (PostgresException ex) when (ex.SqlState == "22012") { }
                await Check("SQL failure after DDL rolls back atomically", false, before);
                await Migrate(db, Forward, commit: false);
                await Check("explicit forward transaction rollback", false, before);
                await Migrate(db, Forward);
                await Check("forward", true, before);
                await Migrate(db, Forward);
                await Check("forward repeated", true, before);
                await schema.CreateAllAsync();
                await schema.CreateAllAsync();
                await Check("full schema reapplied twice", true, before);
                await Append();
                before = await Fingerprint(db);
                await Migrate(db, Reverse, commit: false);
                await Check("explicit reverse transaction rollback", true, before);
                await Migrate(db, Reverse);
                await Check("reverse", false, before);
                await Migrate(db, Reverse);
                await Check("reverse repeated", false, before);
                await schema.CreateAllAsync();
                await Check("full schema after reverse", false, before);
                await Append();
                before = await Fingerprint(db);
                await Migrate(db, Forward);
                await schema.CreateAllAsync();
                await Check("forward after reverse and reapplication", true, before);
                await Append();
                success = true;
            }
            finally
            {
                if (success) await Sql(admin, $"DROP DATABASE {database} WITH (FORCE)");
                else Console.Error.WriteLine("Retained failed fixture: " + database);
            }
        }
        Console.WriteLine($"Complete: {results.Count} migration checks passed. Production unchanged. {output}");
        await File.WriteAllTextAsync(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(Timings, new JsonSerializerOptions { WriteIndented = true }));
    }

    static async Task Migrate(NpgsqlConnection db, string sql, bool commit = true)
    {
        var timer = Stopwatch.StartNew();
        await using var transaction = await db.BeginTransactionAsync();
        await Sql(db, "SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='30s'; LOCK TABLE public.event_log IN ACCESS EXCLUSIVE MODE");
        await Sql(db, sql);
        if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        Timings.Add(new { Database = db.Database, Direction = sql == Forward ? "forward" : "reverse", Commit = commit, Seconds = timer.Elapsed.TotalSeconds });
    }

    static async Task<string> Fingerprint(NpgsqlConnection db) =>
        (string)(await Sql(db, """
            SELECT md5(
                COALESCE((SELECT string_agg(md5(row_to_json(e)::text),',' ORDER BY eventversion) FROM event_log e),'') ||
                COALESCE((SELECT string_agg(md5(row_to_json(m)::text),',' ORDER BY eventid,projectorname) FROM event_projector_state m),'') ||
                COALESCE((SELECT string_agg(md5(row_to_json(c)::text),',' ORDER BY commandid) FROM command_log c),''))
            """))!;
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
