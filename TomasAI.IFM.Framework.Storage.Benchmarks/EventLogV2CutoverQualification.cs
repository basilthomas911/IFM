using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Framework.Storage.Benchmarks;

/// <summary>
/// Destructive production-routing cutover/rollback rehearsal. It owns one uniquely named database on the
/// separately labelled event-log benchmark container and has no code path that accepts an application database.
/// </summary>
internal static class EventLogV2CutoverQualification
{
    internal const string AuthorizationVariable = "IFM_EVENTLOG_BENCHMARK_ADMIN";
    const long DrainLock = 2026092006;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static readonly DateTime FixtureTimestamp = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    const string InitialCopySql = """
        INSERT INTO public.event_log_v2
            (eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp)
        SELECT eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp
        FROM public.event_log
        ON CONFLICT (eventstreamid,streamversion) DO NOTHING;
        """;

    const string ReverseSyncSql = """
        SET LOCAL ifm.event_log_legacy_reverse_sync = 'on';
        INSERT INTO public.event_log
            (eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp)
        SELECT eventstreamid,eventnameid,eventversion,streamversion,eventpayload,commandid,eventtimestamp
        FROM public.event_log_v2
        ON CONFLICT (eventstreamid,eventnameid,eventversion) DO NOTHING;
        """;

    const string LegacyGuardSql = """
        CREATE OR REPLACE FUNCTION public.ifm_qualification_guard_legacy_event_log()
        RETURNS trigger LANGUAGE plpgsql AS $guard$
        BEGIN
          IF current_setting('ifm.event_log_legacy_reverse_sync', true) IS DISTINCT FROM 'on' THEN
            RAISE EXCEPTION 'event_log is frozen during the v2 authority window' USING ERRCODE='55000';
          END IF;
          IF TG_OP='DELETE' THEN RETURN OLD; END IF;
          RETURN NEW;
        END $guard$;
        DROP TRIGGER IF EXISTS ifm_qualification_legacy_write_guard ON public.event_log;
        CREATE TRIGGER ifm_qualification_legacy_write_guard
          BEFORE INSERT OR UPDATE OR DELETE ON public.event_log
          FOR EACH ROW EXECUTE FUNCTION public.ifm_qualification_guard_legacy_event_log();
        """;

    const string V2GuardSql = """
        CREATE OR REPLACE FUNCTION public.ifm_qualification_guard_event_log_v2()
        RETURNS trigger LANGUAGE plpgsql AS $guard$
        BEGIN
          RAISE EXCEPTION 'event_log_v2 is frozen after rollback' USING ERRCODE='55000';
        END $guard$;
        DROP TRIGGER IF EXISTS ifm_qualification_v2_write_guard ON public.event_log_v2;
        CREATE TRIGGER ifm_qualification_v2_write_guard
          BEFORE INSERT OR UPDATE OR DELETE ON public.event_log_v2
          FOR EACH ROW EXECUTE FUNCTION public.ifm_qualification_guard_event_log_v2();
        """;

    sealed record StepResult(string Name, DateTime StartedUtc, DateTime FinishedUtc, bool Passed, string Detail);
    sealed record TableSnapshot(long Count, long? MinimumEventVersion, long? MaximumEventVersion, string Hash);
    sealed record ForeignKeyShape(string Schema, string Table, string Name, string Definition,
        bool Validated, bool Deferrable, bool InitiallyDeferred);
    sealed record Stream(string Name, long Id, long Version);

    internal static async Task RunAsync(string[] args)
    {
        if (args.Any(a => !a.StartsWith("--output=", StringComparison.Ordinal)))
            throw new ArgumentException("Options: --output=PATH");
        if (args.Count(a => a.StartsWith("--output=", StringComparison.Ordinal)) > 1)
            throw new ArgumentException("Specify --output once.");
        var authorization = Environment.GetEnvironmentVariable(AuthorizationVariable);
        if (authorization is null || !(authorization.Equals("true", StringComparison.OrdinalIgnoreCase) || authorization == "1"))
            throw new InvalidOperationException($"Set {AuthorizationVariable}=true to authorize the isolated destructive rehearsal.");

        var raw = Environment.GetEnvironmentVariable(EventLogV2Benchmark.AdminVariable)
            ?? throw new InvalidOperationException($"Set {EventLogV2Benchmark.AdminVariable} to the isolated administration endpoint.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(raw) { Pooling = false };
        if (adminBuilder.Host != "127.0.0.1" || adminBuilder.Port == 5432 || adminBuilder.Database != "postgres")
            throw new InvalidOperationException(
                "Cutover qualification requires postgres on 127.0.0.1 at an explicit non-5432 port with database postgres.");

        var run = Guid.NewGuid().ToString("N")[..12];
        var database = $"ifm_eventlog_bench_{run}_cutover";
        var output = Path.GetFullPath(args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.Ordinal))?[9..]
            ?? Path.Combine("BenchmarkDotNet.Artifacts", "event-log-v2", "cutover-" + run));
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException("Evidence output must be new or empty.");
        Directory.CreateDirectory(output);
        var evidence = new Evidence(output, run, database, Redact(adminBuilder));
        await evidence.InitializeAsync();

        var created = false;
        var completed = false;
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        try
        {
            await evidence.StepAsync("safety-preflight", async () =>
            {
                await EventLogProcessQualification.ValidateContainer(adminBuilder.ConnectionString);
                await admin.OpenAsync();
                Require(Convert.ToInt64(await Scalar(admin,
                    "SELECT count(*) FROM pg_database WHERE NOT datistemplate AND datname<>'postgres'")) == 0,
                    "The isolated server is not empty; no existing database may be touched.");
                Require((string)(await Scalar(admin,
                    "SELECT current_setting('fsync')||'/'||current_setting('synchronous_commit')||'/'||current_setting('full_page_writes')"))!
                    == "on/on/on", "PostgreSQL durability must remain on/on/on.");
                return "Authorization, labelled container identity, loopback endpoint, empty server, and durability verified.";
            });

            await evidence.StepAsync("create-owned-database", async () =>
            {
                Require(Regex.IsMatch(database, @"\Aifm_eventlog_bench_[a-f0-9]{12}_cutover\z"),
                    "Generated database name violated the ownership allowlist.");
                await Execute(admin, $"CREATE DATABASE \"{database}\"");
                created = true;
                return database;
            });

            var directBuilder = new NpgsqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = database,
                Pooling = false,
                ApplicationName = "EventLogV2CutoverQualification"
            };
            var direct = directBuilder.ConnectionString;
            var providerBuilder = new NpgsqlConnectionStringBuilder(directBuilder.ConnectionString);
            providerBuilder.Remove("Username");
            providerBuilder.Remove("Password");
            var provider = providerBuilder.ConnectionString;
            await using var db = new NpgsqlConnection(direct);
            await db.OpenAsync();
            var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
                provider, "System.Data.Postgres");

            await evidence.StepAsync("create-authoritative-legacy-schema", async () =>
            {
                var schema = new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance,
                    new EventLogPersistenceOptions { TableTarget = EventLogTableTarget.Legacy });
                await schema.CreateAllAsync();
                await Execute(db, PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01);
                await Execute(db, EventSourceSchemaSql.CreateEventLogV2Table);
                await VerifyV2Shape(db);
                await evidence.SchemaAsync(db, "schema-before-cutover.json");
                return "Legacy schema initialized through EventSourceSchemaDb; v2 has one PK backing index and exactly three secondary indexes.";
            });

            List<Stream> streams = [];
            var eventNameId = 0;
            await evidence.StepAsync("seed-legacy-through-production-routing", async () =>
            {
                eventNameId = Convert.ToInt32(await Scalar(db, """
                    INSERT INTO event_name_id(eventname,eventtypename) VALUES($1,$2)
                    ON CONFLICT(eventname,eventtypename) DO UPDATE SET eventname=EXCLUDED.eventname
                    RETURNING eventnameid
                    """, nameof(EventLogV2Benchmark.BenchmarkEvent),
                    typeof(EventLogV2Benchmark.BenchmarkEvent).AssemblyQualifiedName!));
                foreach (var name in new[] { "CutoverQualification.Alpha", "CutoverQualification.Beta" })
                {
                    var id = Convert.ToInt64(await Scalar(db,
                        "INSERT INTO event_stream_id(eventstream) VALUES($1) RETURNING eventstreamid", name));
                    streams.Add(new(name, id, 0));
                }
                await using var writer = Writer(provider, EventLogTableTarget.Legacy);
                for (var i = 0; i < streams.Count; i++)
                {
                    var request = Request(streams[i], eventNameId, 3, "legacy-seed");
                    var result = await writer.AppendAsync(request);
                    streams[i] = streams[i] with { Version = result.Assignments[^1].StreamVersion };
                }
                var firstEvent = Convert.ToInt64(await Scalar(db, "SELECT min(eventversion) FROM event_log"));
                await Execute(db,
                    "INSERT INTO business_subscription_projection_receipt(event_id,handoff_completed) VALUES($1,true)", firstEvent);
                await VerifyReplayAsync(settings, direct, EventLogTableTarget.Legacy, streams, expectedCount: 6);
                await evidence.CountsAsync(db, "counts-seeded.json");
                return "Six authoritative events, two command audits, projector markers, and a dependent receipt were committed through the real legacy appender/read routing.";
            });

            TableSnapshot frozenLegacy = null!;
            List<ForeignKeyShape> originalForeignKeys = [];
            await evidence.StepAsync("cutover-stop-copy-retarget-freeze", async () =>
            {
                await RequireNoOtherSessions(db);
                await Execute(db, InitialCopySql);
                await using var transaction = await db.BeginTransactionAsync();
                await Execute(db, "SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='60s';", transaction);
                await Scalar(db, $"SELECT pg_advisory_xact_lock({DrainLock})", transaction);
                await RequireNoOtherSessions(db, transaction);
                await Execute(db, "LOCK TABLE public.event_log,public.event_log_v2 IN ACCESS EXCLUSIVE MODE", transaction);
                // Final delta is intentionally repeated under the drain locks; ON CONFLICT is identity preserving, never dual-write.
                await Execute(db, InitialCopySql, transaction);
                originalForeignKeys = await IncomingForeignKeys(db, "event_log", transaction);
                Require(originalForeignKeys.Count > 0, "No incoming legacy foreign keys were discovered.");
                await RetargetForeignKeys(db, originalForeignKeys, "event_log", "event_log_v2", transaction);
                await AdvanceSequence(db, transaction);
                await Execute(db, LegacyGuardSql, transaction);
                Require(await Snapshot(db, "event_log", transaction) == await Snapshot(db, "event_log_v2", transaction),
                    "Source/target parity failed before activation.");
                await transaction.CommitAsync();
                frozenLegacy = await Snapshot(db, "event_log");
                await evidence.WriteAsync("foreign-keys-before-cutover.json", originalForeignKeys);
                var cutoverForeignKeys = await IncomingForeignKeys(db, "event_log_v2");
                VerifyRetargetedForeignKeys(originalForeignKeys, cutoverForeignKeys, "event_log", "event_log_v2");
                await evidence.WriteAsync("foreign-keys-after-cutover.json", cutoverForeignKeys);
                await File.WriteAllTextAsync(Path.Combine(output, "retarget-foreign-keys-to-v2.sql"),
                    RetargetScript(originalForeignKeys, "event_log", "event_log_v2"));
                await evidence.SchemaAsync(db, "schema-after-cutover.json");
                return $"Drain advisory/ACCESS EXCLUSIVE locks held with zero other sessions; copied {frozenLegacy.Count} rows, advanced the shared sequence, retargeted {originalForeignKeys.Count} FKs, and froze legacy.";
            });

            var v2Options = new EventLogPersistenceOptions
            {
                WriteMode = EventLogWriteMode.BinaryCopy,
                UseLz4Compression = true,
                TableTarget = EventLogTableTarget.EventLogV2
            }.Validate();
            await evidence.WriteAsync("routing-v2.json", new { v2Options.TableTarget, v2Options.WriteMode, v2Options.UseLz4Compression });

            await evidence.StepAsync("activate-v2-append-read-replay", async () =>
            {
                var freshSchema = new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance, v2Options);
                await freshSchema.CreateAllAsync();
                await VerifyV2Shape(db);
                await using (var writer = new BinaryCopyEventLogAppender(provider, true, v2Options))
                {
                    var request = Request(streams[0], eventNameId, 2, "post-cutover");
                    var result = await writer.AppendAsync(request);
                    streams[0] = streams[0] with { Version = result.Assignments[^1].StreamVersion };
                }
                // A freshly constructed journal and writer prove the option is not retained in an old context.
                await VerifyReplayAsync(settings, direct, EventLogTableTarget.EventLogV2, streams, expectedCount: 8);
                await using (var fresh = Writer(provider, EventLogTableTarget.EventLogV2))
                {
                    var request = Request(streams[1], eventNameId, 1, "fresh-context");
                    var result = await fresh.AppendAsync(request);
                    streams[1] = streams[1] with { Version = result.Assignments[^1].StreamVersion };
                }
                var postEvent = Convert.ToInt64(await Scalar(db, "SELECT max(eventversion) FROM event_log_v2"));
                await Execute(db,
                    "INSERT INTO business_subscription_projection_issue(event_id,reason_code,detail) VALUES($1,'QualificationProbe','post-cutover dependent state')",
                    postEvent);
                await VerifyReplayAsync(settings, direct, EventLogTableTarget.EventLogV2, streams, expectedCount: 9);
                await VerifyDependentState(db, "event_log_v2");
                Require(await Snapshot(db, "event_log") == frozenLegacy, "Frozen legacy changed after v2 activation.");
                await evidence.CountsAsync(db, "counts-after-v2-activation.json");
                return "Real v2 BinaryCopy append, audit, projector markers, journal reads, deterministic replay, fresh writer context, and dependent issue row passed; legacy stayed unchanged.";
            });

            await evidence.StepAsync("owned-postgres-restart-on-v2", async () =>
            {
                await EventLogProcessQualification.RestartOwnedPostgresGracefully(db, direct, output);
                await admin.CloseAsync();
                await admin.OpenAsync();
                await VerifyReplayAsync(settings, direct, EventLogTableTarget.EventLogV2, streams, expectedCount: 9);
                Require(await Snapshot(db, "event_log") == frozenLegacy, "Legacy changed across PostgreSQL restart.");
                return "The prevalidated isolated container restarted gracefully; fresh v2 reads/replay and frozen-legacy proof passed.";
            });

            TableSnapshot v2BeforeRollbackAppend = null!;
            await evidence.StepAsync("rollback-stop-reverse-sync-retarget", async () =>
            {
                await RequireNoOtherSessions(db);
                await using var transaction = await db.BeginTransactionAsync();
                await Execute(db, "SET LOCAL lock_timeout='5s'; SET LOCAL statement_timeout='60s';", transaction);
                await Scalar(db, $"SELECT pg_advisory_xact_lock({DrainLock})", transaction);
                await RequireNoOtherSessions(db, transaction);
                await Execute(db, "LOCK TABLE public.event_log,public.event_log_v2 IN ACCESS EXCLUSIVE MODE", transaction);
                await Execute(db, ReverseSyncSql, transaction);
                var active = await IncomingForeignKeys(db, "event_log_v2", transaction);
                Require(active.Count == originalForeignKeys.Count, "The active incoming FK inventory changed before rollback.");
                VerifyRetargetedForeignKeys(originalForeignKeys, active, "event_log", "event_log_v2");
                await RetargetForeignKeys(db, active, "event_log_v2", "event_log", transaction);
                await AdvanceSequence(db, transaction);
                await Execute(db, "DROP TRIGGER if exists ifm_qualification_legacy_write_guard ON public.event_log", transaction);
                await Execute(db, V2GuardSql, transaction);
                Require(await Snapshot(db, "event_log", transaction) == await Snapshot(db, "event_log_v2", transaction),
                    "Reverse synchronization parity failed.");
                await transaction.CommitAsync();
                v2BeforeRollbackAppend = await Snapshot(db, "event_log_v2");
                var restored = await IncomingForeignKeys(db, "event_log");
                VerifyRetargetedForeignKeys(originalForeignKeys, restored, "event_log", "event_log");
                await VerifyDependentState(db, "event_log");
                await evidence.WriteAsync("foreign-keys-after-rollback.json", restored);
                await File.WriteAllTextAsync(Path.Combine(output, "retarget-foreign-keys-to-legacy.sql"),
                    RetargetScript(active, "event_log_v2", "event_log"));
                await evidence.SchemaAsync(db, "schema-after-rollback.json");
                await evidence.CountsAsync(db, "counts-after-reverse-sync.json");
                return $"Reverse-synchronized all {v2BeforeRollbackAppend.Count} events and shared audit/marker/dependent state, restored all {active.Count} FKs and sequence safety, unfroze legacy, and froze v2.";
            });

            var legacyOptions = new EventLogPersistenceOptions
            {
                WriteMode = EventLogWriteMode.BinaryCopy,
                UseLz4Compression = true,
                TableTarget = EventLogTableTarget.Legacy
            }.Validate();
            await evidence.WriteAsync("routing-rollback.json", new { legacyOptions.TableTarget, legacyOptions.WriteMode, legacyOptions.UseLz4Compression });
            await evidence.StepAsync("activate-rollback-append-read-replay", async () =>
            {
                await using (var writer = new BinaryCopyEventLogAppender(provider, true, legacyOptions))
                {
                    var request = Request(streams[0], eventNameId, 1, "post-rollback");
                    var result = await writer.AppendAsync(request);
                    streams[0] = streams[0] with { Version = result.Assignments[^1].StreamVersion };
                }
                await VerifyReplayAsync(settings, direct, EventLogTableTarget.Legacy, streams, expectedCount: 10);
                Require(await Snapshot(db, "event_log_v2") == v2BeforeRollbackAppend,
                    "v2 changed after rollback authority was activated.");
                var rejected = false;
                try
                {
                    await using var forbidden = Writer(provider, EventLogTableTarget.EventLogV2);
                    await forbidden.AppendAsync(Request(streams[1], eventNameId, 1, "forbidden-v2"));
                }
                catch (PostgresException ex) when (ex.SqlState == "55000") { rejected = true; }
                Require(rejected, "The post-rollback v2 write guard did not reject a normal production-routed append.");
                Require(await Snapshot(db, "event_log_v2") == v2BeforeRollbackAppend,
                    "Rejected v2 write changed the rolled-back target.");
                await evidence.CountsAsync(db, "counts-final.json");
                return "Fresh legacy routing appended/read/replayed after rollback; the v2 guard rejected a normal routed append and v2 remained byte/count unchanged.";
            });

            await evidence.StepAsync("final-inventory-and-manifest", async () =>
            {
                await VerifyV2Shape(db);
                await evidence.PathInventoryAsync();
                await evidence.WriteAsync("environment-redacted.json", RedactedEnvironment());
                await evidence.CompleteManifestAsync("passed", databaseDropped: false,
                    "All rehearsal assertions passed; database cleanup is the remaining step.");
                return "Schema, counts/hashes, SQL, routing/path inventory, timestamps, step results, and redacted environment captured.";
            });

            await db.CloseAsync();
            await evidence.StepAsync("drop-successful-owned-database", async () =>
            {
                await Execute(admin, $"DROP DATABASE \"{database}\" WITH (FORCE)");
                created = false;
                return "Successful uniquely owned qualification database dropped.";
            });
            completed = true;
            await evidence.CompleteManifestAsync("passed", databaseDropped: true,
                "Qualification passed and the owned database was dropped. Evidence is retained.");
            Console.WriteLine($"Event-log v2 cutover/rollback qualification passed. Evidence: {output}");
        }
        catch (Exception ex)
        {
            await evidence.RecordFailureAsync(ex, created);
            throw;
        }
        finally
        {
            if (!completed && created)
                Console.Error.WriteLine($"Retained failed isolated database {database} and evidence {output}");
        }
    }

    static BinaryCopyEventLogAppender Writer(string connection, EventLogTableTarget target) =>
        new(connection, true, new EventLogPersistenceOptions
        {
            WriteMode = EventLogWriteMode.BinaryCopy,
            UseLz4Compression = true,
            TableTarget = target
        });

    static EventLogAppendRequest Request(Stream stream, int eventNameId, int count, string phase)
    {
        var commandId = Guid.NewGuid();
        var command = new EventLogV2Benchmark.BenchmarkCommand
        {
            CommandId = commandId,
            StreamId = stream.Name,
            Value = stream.Version + count,
            Payload = phase
        };
        var events = Enumerable.Range(1, count).Select(offset => new EventLogAppendEntry(eventNameId,
            new EventLogV2Benchmark.BenchmarkEvent
            {
                CommandId = commandId,
                AggregateId = stream.Name,
                Value = stream.Version + offset,
                Payload = phase,
                RequiresDurableProjection = true
            })).ToArray();
        return new(stream.Name, stream.Id, commandId, events, stream.Version, FixtureTimestamp.AddMinutes(stream.Version),
            CommandAuditEnvelope.Create(command, new CommandAuditMessagePackCodec()));
    }

    static async Task VerifyReplayAsync(IDbConnectionSettings settings, string direct, EventLogTableTarget target,
        IReadOnlyList<Stream> streams, long expectedCount)
    {
        var options = new EventLogPersistenceOptions { TableTarget = target, UseLz4Compression = true }.Validate();
        var journal = new PostgresCommittedBusinessEventJournal(settings, options);
        foreach (var stream in streams)
        {
            var prior = await journal.ReadPriorAsync(stream.Id, long.MaxValue,
                [nameof(EventLogV2Benchmark.BenchmarkEvent)], CancellationToken.None);
            Require(prior is not null && prior.StreamVersion == stream.Version,
                $"Production journal did not read the current event for {stream.Name}.");
        }

        await using var db = new NpgsqlConnection(direct);
        await db.OpenAsync();
        var table = target == EventLogTableTarget.Legacy ? "event_log" : "event_log_v2";
        var codec = new EventLogMessagePackCodec(true);
        var versions = new Dictionary<long, long>();
        var timestamps = new Dictionary<long, DateTime>();
        long count = 0;
        var replaySql = "SELECT eventstreamid,eventversion,streamversion,eventpayload,commandid,eventtimestamp " +
            $"FROM {table} ORDER BY eventstreamid,streamversion";
        await using var command = new NpgsqlCommand(replaySql, db);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var streamId = reader.GetInt64(0);
            var expectedVersion = versions.GetValueOrDefault(streamId) + 1;
            var value = (EventLogV2Benchmark.BenchmarkEvent)codec.Deserialize(
                typeof(EventLogV2Benchmark.BenchmarkEvent).AssemblyQualifiedName!, reader.GetInt64(1),
                reader.GetFieldValue<byte[]>(3));
            var timestamp = DateTime.Parse(reader.GetString(5), null,
                System.Globalization.DateTimeStyles.RoundtripKind);
            Require(reader.GetInt64(2) == expectedVersion && value.Value == expectedVersion &&
                value.CommandId == reader.GetGuid(4) && timestamp.Kind == DateTimeKind.Utc &&
                timestamp >= timestamps.GetValueOrDefault(streamId, DateTime.MinValue),
                "Ordered replay identity/version/timestamp verification failed.");
            versions[streamId] = expectedVersion;
            timestamps[streamId] = timestamp;
            count++;
        }
        Require(count == expectedCount && streams.All(s => versions.GetValueOrDefault(s.Id) == s.Version),
            "Deterministic replay count/final-state mismatch.");
    }

    static async Task VerifyV2Shape(NpgsqlConnection db)
    {
        var primary = (string)(await Scalar(db,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid='public.event_log_v2'::regclass AND contype='p'"))!;
        Require(primary == "PRIMARY KEY (eventstreamid, streamversion)", "Unexpected v2 primary key: " + primary);
        Require(Convert.ToInt32(await Scalar(db,
            "SELECT count(*) FROM pg_index WHERE indrelid='public.event_log_v2'::regclass AND NOT indisprimary")) == 3,
            "event_log_v2 must have exactly three secondary indexes in addition to its PK backing index.");
        const string indexManifestSql = "SELECT string_agg(indexrelid::regclass::text,',' ORDER BY indexrelid::regclass::text) " +
            "FROM pg_index WHERE indrelid='public.event_log_v2'::regclass AND NOT indisprimary";
        var names = (string)(await Scalar(db, indexManifestSql))!;
        Require(names == "ix_event_log_v2_command_id,ix_event_log_v2_event_name_version,ux_event_log_v2_event_version",
            "Unexpected v2 secondary-index manifest: " + names);
    }

    static async Task<List<ForeignKeyShape>> IncomingForeignKeys(NpgsqlConnection db, string table,
        NpgsqlTransaction? transaction = null)
    {
        var result = new List<ForeignKeyShape>();
        await using var command = new NpgsqlCommand("""
            SELECT n.nspname,c.relname,k.conname,pg_get_constraintdef(k.oid),
                   k.convalidated,k.condeferrable,k.condeferred
            FROM pg_constraint k
            JOIN pg_class c ON c.oid=k.conrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE k.contype='f' AND k.confrelid=to_regclass('public.'||$1)
            ORDER BY n.nspname,c.relname,k.conname
            """, db, transaction);
        command.Parameters.AddWithValue(table);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6)));
        return result;
    }

    static async Task RetargetForeignKeys(NpgsqlConnection db, IReadOnlyList<ForeignKeyShape> shapes,
        string source, string target, NpgsqlTransaction transaction)
    {
        foreach (var shape in shapes)
        {
            var definition = Regex.Replace(shape.Definition,
                $@"REFERENCES\s+(?:public\.)?{Regex.Escape(source)}\s*\(",
                $"REFERENCES public.{target}(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            Require(definition != shape.Definition, $"Could not safely retarget FK {shape.Schema}.{shape.Table}.{shape.Name}.");
            var owner = $"{Quote(shape.Schema)}.{Quote(shape.Table)}";
            await Execute(db, $"ALTER TABLE {owner} DROP CONSTRAINT {Quote(shape.Name)}", transaction);
            await Execute(db, $"ALTER TABLE {owner} ADD CONSTRAINT {Quote(shape.Name)} {definition} NOT VALID", transaction);
            if (shape.Validated)
                await Execute(db, $"ALTER TABLE {owner} VALIDATE CONSTRAINT {Quote(shape.Name)}", transaction);
        }
    }

    static string RetargetScript(IReadOnlyList<ForeignKeyShape> shapes, string source, string target)
    {
        var sql = new StringBuilder();
        sql.AppendLine("-- Generated from pg_constraint for this owned qualification database.");
        foreach (var shape in shapes)
        {
            var definition = Regex.Replace(shape.Definition,
                $@"REFERENCES\s+(?:public\.)?{Regex.Escape(source)}\s*\(",
                $"REFERENCES public.{target}(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var owner = $"{Quote(shape.Schema)}.{Quote(shape.Table)}";
            sql.AppendLine($"ALTER TABLE {owner} DROP CONSTRAINT {Quote(shape.Name)};");
            sql.AppendLine($"ALTER TABLE {owner} ADD CONSTRAINT {Quote(shape.Name)} {definition} NOT VALID;");
            if (shape.Validated)
                sql.AppendLine($"ALTER TABLE {owner} VALIDATE CONSTRAINT {Quote(shape.Name)};");
        }
        return sql.ToString();
    }

    static void VerifyRetargetedForeignKeys(IReadOnlyList<ForeignKeyShape> original,
        IReadOnlyList<ForeignKeyShape> actual, string source, string target)
    {
        Require(original.Count == actual.Count, "Incoming FK count changed during retargeting.");
        foreach (var before in original)
        {
            var after = actual.SingleOrDefault(x => x.Schema == before.Schema && x.Table == before.Table && x.Name == before.Name)
                ?? throw new InvalidOperationException($"Incoming FK disappeared: {before.Schema}.{before.Table}.{before.Name}.");
            var expected = source == target ? before.Definition : Regex.Replace(before.Definition,
                $@"REFERENCES\s+(?:public\.)?{Regex.Escape(source)}\s*\(",
                $"REFERENCES {target}(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            // PostgreSQL omits the explicit public schema when deparsing a relation on the search path.
            static string Normalize(string value) => value.Replace("REFERENCES public.", "REFERENCES ", StringComparison.OrdinalIgnoreCase);
            Require(Normalize(after.Definition) == Normalize(expected) &&
                after.Validated == before.Validated && after.Deferrable == before.Deferrable &&
                after.InitiallyDeferred == before.InitiallyDeferred,
                $"Incoming FK semantics changed: {before.Schema}.{before.Table}.{before.Name}.");
        }
    }

    static async Task VerifyDependentState(NpgsqlConnection db, string table)
    {
        Require(table is "event_log" or "event_log_v2", "Dependent-state authority is not allowlisted.");
        var orphaned = Convert.ToInt64(await Scalar(db, $"""
            SELECT
              (SELECT count(*) FROM event_projector_state x LEFT JOIN {table} e ON e.eventversion=x.eventid WHERE e.eventversion IS NULL) +
              (SELECT count(*) FROM business_subscription_projection_receipt x LEFT JOIN {table} e ON e.eventversion=x.event_id WHERE e.eventversion IS NULL) +
              (SELECT count(*) FROM business_subscription_projection_issue x LEFT JOIN {table} e ON e.eventversion=x.event_id WHERE e.eventversion IS NULL) +
              (SELECT count(*) FROM command_log c WHERE NOT EXISTS(SELECT 1 FROM {table} e WHERE e.commandid=c.commandid))
            """));
        Require(orphaned == 0, $"Dependent audit/marker/journal state is not complete for {table}.");
    }

    static async Task AdvanceSequence(NpgsqlConnection db, NpgsqlTransaction transaction)
    {
        var maximum = Convert.ToInt64(await Scalar(db,
            "SELECT greatest(coalesce(max(eventversion),0),(SELECT coalesce(max(eventversion),0) FROM event_log_v2)) FROM event_log",
            transaction));
        Require(maximum > 0, "Cannot initialize the shared sequence from an empty authority set.");
        await Scalar(db, "SELECT setval('public.event_log_eventversion_seq',$1,true)", transaction, maximum);
        var last = Convert.ToInt64(await Scalar(db, "SELECT last_value FROM public.event_log_eventversion_seq", transaction));
        Require(last == maximum, "Shared sequence was not advanced to the exact high-water mark.");
    }

    static async Task RequireNoOtherSessions(NpgsqlConnection db, NpgsqlTransaction? transaction = null)
    {
        var sessions = Convert.ToInt64(await Scalar(db, """
            SELECT count(*) FROM pg_stat_activity
            WHERE datname=current_database() AND pid<>pg_backend_pid()
            """, transaction));
        Require(sessions == 0, $"Drain failed: {sessions} other database session(s) remain.");
    }

    static async Task<TableSnapshot> Snapshot(NpgsqlConnection db, string table,
        NpgsqlTransaction? transaction = null)
    {
        Require(table is "event_log" or "event_log_v2", "Snapshot table is not allowlisted.");
        await using var command = new NpgsqlCommand($"""
            SELECT count(*),min(eventversion),max(eventversion),
              md5(coalesce(string_agg(md5(
                eventstreamid::text||chr(31)||eventnameid::text||chr(31)||eventversion::text||chr(31)||
                streamversion::text||chr(31)||encode(eventpayload,'hex')||chr(31)||commandid::text||chr(31)||eventtimestamp
              ),'' ORDER BY eventversion),''))
            FROM {table}
            """, db, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new(reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1),
            reader.IsDBNull(2) ? null : reader.GetInt64(2), reader.GetString(3));
    }

    static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"") + '"';

    static async Task<object?> Scalar(NpgsqlConnection db, string sql, params object[] parameters)
        => await Scalar(db, sql, null, parameters);

    static async Task<object?> Scalar(NpgsqlConnection db, string sql, NpgsqlTransaction? transaction,
        params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, db, transaction) { CommandTimeout = 120 };
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync();
    }

    static Task Execute(NpgsqlConnection db, string sql, params object[] parameters)
        => Execute(db, sql, null, parameters);

    static async Task Execute(NpgsqlConnection db, string sql, NpgsqlTransaction? transaction,
        params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, db, transaction) { CommandTimeout = 120 };
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }

    static string Redact(NpgsqlConnectionStringBuilder builder)
    {
        var redacted = new NpgsqlConnectionStringBuilder(builder.ConnectionString);
        if (!string.IsNullOrEmpty(redacted.Password)) redacted.Password = "<redacted>";
        return redacted.ConnectionString;
    }

    static object RedactedEnvironment() => new
    {
        CapturedAtUtc = DateTime.UtcNow,
        MachineName = "<redacted>",
        UserName = "<redacted>",
        Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        ProcessorCount = Environment.ProcessorCount,
        Variables = new Dictionary<string, object?>
        {
            [AuthorizationVariable] = "<present-redacted>",
            [EventLogV2Benchmark.AdminVariable] = "<present-redacted>",
            ["DOTNET_ENVIRONMENT"] = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            ["ASPNETCORE_ENVIRONMENT"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        }
    };

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    sealed class Evidence(string output, string run, string database, string redactedAdmin)
    {
        readonly List<StepResult> steps = [];
        readonly DateTime started = DateTime.UtcNow;

        internal async Task InitializeAsync()
        {
            await File.WriteAllTextAsync(Path.Combine(output, "create-event-log-v2.sql"), EventSourceSchemaSql.CreateEventLogV2Table);
            await File.WriteAllTextAsync(Path.Combine(output, "initial-and-final-delta-copy.sql"), InitialCopySql);
            await File.WriteAllTextAsync(Path.Combine(output, "reverse-sync.sql"), ReverseSyncSql);
            await File.WriteAllTextAsync(Path.Combine(output, "legacy-write-guard.sql"), LegacyGuardSql);
            await File.WriteAllTextAsync(Path.Combine(output, "post-rollback-v2-write-guard.sql"), V2GuardSql);
            await File.WriteAllTextAsync(Path.Combine(output, "drain-lock-and-sequence.sql"), $"""
                -- Executed inside the cutover or rollback transaction after zero-other-session verification.
                SELECT pg_advisory_xact_lock({DrainLock});
                LOCK TABLE public.event_log,public.event_log_v2 IN ACCESS EXCLUSIVE MODE;
                SELECT setval('public.event_log_eventversion_seq',
                  (SELECT greatest(coalesce(max(eventversion),0),(SELECT coalesce(max(eventversion),0) FROM event_log_v2)) FROM event_log),true);
                """);
            await WriteAsync("environment-redacted.json", RedactedEnvironment());
            await CompleteManifestAsync("running", false, "Qualification is in progress; this is not passing evidence.");
        }

        internal async Task StepAsync(string name, Func<Task<string>> action)
        {
            var begin = DateTime.UtcNow;
            try
            {
                var detail = await action();
                steps.Add(new(name, begin, DateTime.UtcNow, true, detail));
                await WriteAsync("step-results.json", steps);
            }
            catch (Exception ex)
            {
                steps.Add(new(name, begin, DateTime.UtcNow, false, ex.GetType().Name + ": " + ex.Message));
                await WriteAsync("step-results.json", steps);
                throw;
            }
        }

        internal Task WriteAsync<T>(string name, T value) =>
            File.WriteAllTextAsync(Path.Combine(output, name), JsonSerializer.Serialize(value, Json));

        internal async Task CountsAsync(NpgsqlConnection db, string name)
        {
            var legacy = await Snapshot(db, "event_log");
            var v2 = await Snapshot(db, "event_log_v2");
            const string dependentSql = """
                SELECT jsonb_pretty(jsonb_build_object(
                  'command_log_count',(SELECT count(*) FROM command_log),
                  'command_log_hash',(SELECT md5(coalesce(string_agg(md5(row_to_json(x)::text),'' ORDER BY commandid),'')) FROM command_log x),
                  'projector_state_count',(SELECT count(*) FROM event_projector_state),
                  'projector_state_hash',(SELECT md5(coalesce(string_agg(md5(row_to_json(x)::text),'' ORDER BY eventid,projectorname),'')) FROM event_projector_state x),
                  'receipt_count',(SELECT count(*) FROM business_subscription_projection_receipt),
                  'issue_count',(SELECT count(*) FROM business_subscription_projection_issue),
                  'stream_versions',(SELECT jsonb_object_agg(eventstream,currentversion ORDER BY eventstream) FROM event_stream_id)
                ))
                """;
            var dependent = (string)(await Scalar(db, dependentSql))!;
            await WriteAsync(name, new { CapturedAtUtc = DateTime.UtcNow, Legacy = legacy, EventLogV2 = v2,
                DependentState = JsonDocument.Parse(dependent).RootElement.Clone() });
        }

        internal async Task SchemaAsync(NpgsqlConnection db, string name)
        {
            const string schemaSql = """
                SELECT jsonb_pretty(jsonb_build_object(
                  'columns',(SELECT jsonb_agg(jsonb_build_object('table',table_name,'ordinal',ordinal_position,'name',column_name,'type',data_type,'nullable',is_nullable,'default',column_default) ORDER BY table_name,ordinal_position)
                    FROM information_schema.columns WHERE table_schema='public' AND table_name IN ('event_log','event_log_v2')),
                  'indexes',(SELECT jsonb_agg(jsonb_build_object('table',tablename,'name',indexname,'definition',indexdef) ORDER BY tablename,indexname)
                    FROM pg_indexes WHERE schemaname='public' AND tablename IN ('event_log','event_log_v2')),
                  'constraints',(SELECT jsonb_agg(jsonb_build_object('schema',n.nspname,'table',c.relname,'name',k.conname,'type',k.contype,'validated',k.convalidated,'definition',pg_get_constraintdef(k.oid)) ORDER BY n.nspname,c.relname,k.conname)
                    FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE k.conrelid IN ('public.event_log'::regclass,'public.event_log_v2'::regclass)
                       OR k.confrelid IN ('public.event_log'::regclass,'public.event_log_v2'::regclass)),
                  'triggers',(SELECT jsonb_agg(jsonb_build_object('table',c.relname,'name',t.tgname,'enabled',t.tgenabled,'definition',pg_get_triggerdef(t.oid)) ORDER BY c.relname,t.tgname)
                    FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid
                    WHERE NOT t.tgisinternal AND t.tgrelid IN ('public.event_log'::regclass,'public.event_log_v2'::regclass)),
                  'sequence',(SELECT to_jsonb(s) FROM (SELECT last_value,is_called FROM public.event_log_eventversion_seq) s)
                ))
                """;
            var json = (string)(await Scalar(db, schemaSql))!;
            await File.WriteAllTextAsync(Path.Combine(output, name), json);
        }

        internal async Task PathInventoryAsync()
        {
            var inventory = new object[]
            {
                new { Path = "SequentialEventLogAppender", Kind = "append", Proof = "EventLogSqlLayout.ForProduction(options)" },
                new { Path = "BinaryCopyEventLogAppender", Kind = "append/audit/marker", Proof = "EventLogSqlLayout.ForProduction(options)" },
                new { Path = "EventSourceActorDbContext", Kind = "read/replay/admin/projector", Proof = "EventLogSqlLayout.ForProduction(options)" },
                new { Path = "PostgresEventTransaction/EnlistedEventTransaction", Kind = "atomic financial/risk append and all enlisted SQL", Proof = "CreateCommand resolves every SQL statement" },
                new { Path = "PostgresCommittedBusinessEventJournal", Kind = "journal/read/recovery", Proof = "constructor resolves all event joins" },
                new { Path = "EventSourceSchemaDb", Kind = "schema", Proof = "TableTarget selects v2 DDL and resolves dependent event SQL" },
                new { Path = "PostgreSQL pg_constraint inventory", Kind = "every actual incoming FK", Proof = "catalog-driven transactional retarget with original definition/actions/validation" },
                new { Path = "Qualification SQL probes", Kind = "parity/sequence/guards/session drain", Proof = "saved SQL and step evidence" }
            };
            await WriteAsync("path-inventory.json", inventory);
        }

        internal async Task CompleteManifestAsync(string status, bool databaseDropped, string note)
        {
            var sourceHashes = new Dictionary<string, string>();
            foreach (var file in new[]
            {
                "TomasAI.IFM.Framework.Storage.Benchmarks/EventLogV2CutoverQualification.cs",
                "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogPersistenceContracts.cs",
                "TomasAI.IFM.Application.Storage/EventSourceDb/Persistence/EventLogSqlLayout.cs",
                "TomasAI.IFM.Application.Storage/EventSourceDb/Schema/EventSourceSchemaSql.cs"
            })
                if (File.Exists(file)) sourceHashes[file.Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file)));
            await WriteAsync("manifest.json", new
            {
                SchemaVersion = 1,
                RunId = run,
                Status = status,
                StartedUtc = started,
                UpdatedUtc = DateTime.UtcNow,
                Database = database,
                DatabaseDropped = databaseDropped,
                AdminEndpoint = redactedAdmin,
                Command = "--event-log-v2-cutover-qualification",
                AuthorityModel = "single-table routing only; no dual-write",
                EventLogV2IndexManifest = new
                {
                    PrimaryKey = "event_log_v2_pkey (eventstreamid, streamversion)",
                    SecondaryIndexCount = 3,
                    SecondaryIndexes = new[] { "ux_event_log_v2_event_version (eventversion UNIQUE)",
                        "ix_event_log_v2_command_id (commandid)",
                        "ix_event_log_v2_event_name_version (eventnameid,eventversion)" },
                    Clarification = "PostgreSQL therefore reports four index objects: one PK backing index plus exactly three secondary indexes."
                },
                Note = note,
                SourceSha256 = sourceHashes
            });
        }

        internal async Task RecordFailureAsync(Exception exception, bool databaseRetained)
        {
            await WriteAsync("failure.json", new
            {
                FailedAtUtc = DateTime.UtcNow,
                Exception = exception.GetType().FullName,
                exception.Message,
                DatabaseRetained = databaseRetained,
                EvidenceRetained = true
            });
            await CompleteManifestAsync("failed", false,
                databaseRetained ? "Failure retained the owned database and evidence for diagnosis." : "Failure occurred before database ownership was established; evidence is retained.");
        }
    }
}
