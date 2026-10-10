using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Npgsql;
using NpgsqlTypes;
using System.Text;

namespace TomasAI.IFM.Application.Storage.Postgres.Benchmarks;

/// <summary>Real PostgreSQL round trips against disposable Docker databases, including durable WAL commits.</summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[MinIterationTime(500)]
public class PostgresTuningBenchmarks
{
    NpgsqlDataSource source = null!;
    NpgsqlConnection connection = null!;
    NpgsqlCommand append = null!, batch = null!, batch8 = null!, batch128 = null!, snapshot = null!, history = null!, workingSet = null!, sort = null!;
    int nextStream;
    readonly NpgsqlConnection[] concurrentConnections = new NpgsqlConnection[8];
    readonly NpgsqlCommand[] concurrentCommands = new NpgsqlCommand[8];

    public IEnumerable<string> Profiles => [Environment.GetEnvironmentVariable("IFM_PG_TUNING_PROFILE") ?? "Baseline"];
    [ParamsSource(nameof(Profiles))] public string Profile { get; set; } = "";
    public string Version => "18.6";

    [GlobalSetup]
    public async Task Setup()
    {
        var port = int.Parse(Environment.GetEnvironmentVariable("IFM_POSTGRES_BENCHMARK_PORT") ?? "0");
        var host = Environment.GetEnvironmentVariable("IFM_PG_TUNING_HOST") ?? "127.0.0.1";
        if ((host == "127.0.0.1" && port != 58118) || (host != "127.0.0.1" && (host != "pg-tuning" || port != 5432))) throw new InvalidOperationException("Only dedicated benchmark ports are allowed.");
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host, Port = port, Database = "ifm_pg_benchmark", Username = "benchmark",
            Password = Environment.GetEnvironmentVariable("IFM_POSTGRES_BENCHMARK_PASSWORD")
                ?? throw new InvalidOperationException("Benchmark credential is required."),
            Pooling = true, MaxPoolSize = 16, Timeout = 15, CommandTimeout = 30,
            ApplicationName = "IFM.PostgresTuningBenchmark"
        };
        source = NpgsqlDataSource.Create(builder.ConnectionString);
        connection = await source.OpenConnectionAsync();
        using (var check = new NpgsqlCommand("SELECT current_setting('server_version'), current_database(), current_setting('fsync'), current_setting('synchronous_commit'), current_setting('full_page_writes')", connection))
        await using (var reader = await check.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            if (!reader.GetString(0).StartsWith(Version + " ", StringComparison.Ordinal) && reader.GetString(0) != Version)
                throw new InvalidOperationException("Server version does not match requested benchmark version.");
            if (reader.GetString(1) != "ifm_pg_benchmark" || Enumerable.Range(2, 3).Any(i => reader.GetString(i) != "on"))
                throw new InvalidOperationException("Isolated database and full durability settings are required.");
        }
        using var seed = new NpgsqlCommand("""
            DROP SCHEMA IF EXISTS benchmark CASCADE;
            CREATE SCHEMA benchmark;
            CREATE SEQUENCE benchmark.event_version;
            CREATE TABLE benchmark.event_stream_id (eventstreamid bigint PRIMARY KEY, currentversion bigint NOT NULL);
            CREATE TABLE benchmark.event_log (
                eventstreamid bigint NOT NULL, eventnameid integer NOT NULL,
                eventversion bigint DEFAULT nextval('benchmark.event_version') NOT NULL,
                streamversion bigint NOT NULL, eventpayload bytea NOT NULL CHECK(octet_length(eventpayload)>0),
                commandid uuid NOT NULL, eventtimestamp text NOT NULL,
                PRIMARY KEY(eventstreamid, streamversion));
            CREATE UNIQUE INDEX ON benchmark.event_log(eventversion);
            CREATE INDEX ON benchmark.event_log(commandid);
            CREATE INDEX ON benchmark.event_log(eventnameid,eventversion);
            INSERT INTO benchmark.event_stream_id SELECT s,1000 FROM generate_series(1,500) s;
            INSERT INTO benchmark.event_log(eventstreamid,eventnameid,streamversion,eventpayload,commandid,eventtimestamp)
            SELECT s,CASE WHEN v%100=0 THEN 2 ELSE 1 END,v,
                convert_to(lpad(v::text,16,'0') || repeat('x',1008),'UTF8'),'11111111-1111-1111-1111-111111111111','2026-10-09T20:00:00Z'
            FROM generate_series(1,500) s CROSS JOIN generate_series(1,1000) v;
            VACUUM (ANALYZE) benchmark.event_log;
            """, connection) { CommandTimeout = 120 };
        // VACUUM must be its own statement outside a transaction.
        var vacuumAt = seed.CommandText.IndexOf("VACUUM", StringComparison.Ordinal);
        var vacuumSql = seed.CommandText[vacuumAt..];
        seed.CommandText = seed.CommandText[..vacuumAt];
        using var exists = new NpgsqlCommand("SELECT to_regclass('benchmark.event_log') IS NOT NULL", connection);
        if (!(bool)(await exists.ExecuteScalarAsync())!)
            await seed.ExecuteNonQueryAsync();
        else
        {
            // Reuse the identical immutable seed; remove prior case writes before measurement.
            using var reset = new NpgsqlCommand("DELETE FROM benchmark.event_log WHERE streamversion>1000; UPDATE benchmark.event_stream_id SET currentversion=1000; SELECT setval('benchmark.event_version',500000)", connection) { CommandTimeout=120 };
            await reset.ExecuteNonQueryAsync();
        }
        using (var vacuum = new NpgsqlCommand(vacuumSql, connection)) await vacuum.ExecuteNonQueryAsync();
        using (var checkpoint = new NpgsqlCommand("CHECKPOINT",connection)) await checkpoint.ExecuteNonQueryAsync();
        var payload = Encoding.UTF8.GetBytes(new string('x', 1024));
        append = CreateAppend(connection, 1, 1, payload);
        batch = CreateAppend(connection, 1, 32, payload);
        batch8 = CreateAppend(connection, 1, 8, payload);
        batch128 = CreateAppend(connection, 1, 128, payload);
        snapshot = new NpgsqlCommand("SELECT eventpayload FROM benchmark.event_log WHERE eventstreamid=50 AND eventnameid=2 ORDER BY streamversion DESC LIMIT 1", connection);
        history = new NpgsqlCommand("SELECT streamversion,eventpayload FROM benchmark.event_log WHERE eventstreamid=50 ORDER BY streamversion DESC LIMIT 100", connection);
        workingSet = new NpgsqlCommand("SELECT eventpayload FROM benchmark.event_log WHERE eventstreamid=$1 AND streamversion >= $2 ORDER BY streamversion LIMIT 100", connection);
        workingSet.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Bigint, Value=1L });
        workingSet.Parameters.Add(new NpgsqlParameter { NpgsqlDbType=NpgsqlDbType.Bigint, Value=1L });
        sort = new NpgsqlCommand("SELECT sum(streamversion) FROM (SELECT streamversion,eventpayload FROM benchmark.event_log WHERE eventstreamid BETWEEN 1 AND 20 ORDER BY eventpayload,streamversion OFFSET 0) s", connection);
        await batch8.PrepareAsync(); await batch128.PrepareAsync(); await workingSet.PrepareAsync(); await sort.PrepareAsync();
        await append.PrepareAsync(); await batch.PrepareAsync(); await snapshot.PrepareAsync(); await history.PrepareAsync();
        for (var i = 0; i < 8; i++)
        {
            concurrentConnections[i] = await source.OpenConnectionAsync();
            concurrentCommands[i] = CreateAppend(concurrentConnections[i], i + 1, 1, payload);
            await concurrentCommands[i].PrepareAsync();
        }
        // Validate each workload before the measured warmup and iterations.
        await AppendEvent(); await AppendBatch32();
        if (await LatestSnapshot() != 1024 || await History100() != 100) throw new InvalidOperationException("Seed/read validation failed.");
        await ConcurrentAppend8();
        if (await WorkingSetRead() != 100 || await SortWindow() <= 0) throw new InvalidOperationException("Tuning workload validation failed.");
        var evidenceRoot=Environment.GetEnvironmentVariable("IFM_PG_TUNING_EVIDENCE");
        if (evidenceRoot is not null)
        {
            Directory.CreateDirectory(evidenceRoot);
            using var explain=new NpgsqlCommand("EXPLAIN (ANALYZE,BUFFERS,FORMAT JSON) " + sort.CommandText, connection);
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot,Profile+"-sort-plan.json"), (string)(await explain.ExecuteScalarAsync())!);
        }
    }

    /// <summary>Atomically advances the stream and appends the requested number of one-kilobyte event payloads in a durable commit.</summary>
    internal static NpgsqlCommand CreateAppend(NpgsqlConnection connection, long stream, int count, byte[] payload)
    {
        var command = new NpgsqlCommand("""
            WITH version AS (
                UPDATE benchmark.event_stream_id SET currentversion=currentversion+$2
                WHERE eventstreamid=$1 RETURNING currentversion), inserted AS (
                INSERT INTO benchmark.event_log(eventstreamid,eventnameid,streamversion,eventpayload,commandid,eventtimestamp)
                SELECT $1,1,currentversion-$2+n,$3,$4,$5 FROM version CROSS JOIN generate_series(1,$2) n
                RETURNING streamversion)
            SELECT max(streamversion) FROM inserted
            """, connection);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = stream });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Integer, Value = count });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bytea, Value = payload });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = Guid.NewGuid() });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = "2026-10-09T20:00:00Z" });
        return command;
    }

    [Benchmark] public Task<long> AppendEvent() => ExecuteAppend(append);
    static async Task<long> ExecuteAppend(NpgsqlCommand command)
    {
        command.Parameters[3].Value=Guid.NewGuid();
        return (long)(await command.ExecuteScalarAsync())!;
    }
    [Benchmark] public Task<long> AppendBatch32() => ExecuteAppend(batch);
    [Benchmark] public Task<long> AppendBatch8() => ExecuteAppend(batch8);
    [Benchmark] public Task<long> AppendBatch128() => ExecuteAppend(batch128);
    [Benchmark] public async Task<int> WorkingSetRead()
    {
        // Deterministic permutation traverses all 500 streams rather than the same cached stream.
        workingSet.Parameters[0].Value=(long)((nextStream % 500)*137 % 500 + 1);
        workingSet.Parameters[1].Value=(long)((nextStream++ / 500)*97 % 901 + 1);
        await using var reader=await workingSet.ExecuteReaderAsync();
        var count=0;
        while(await reader.ReadAsync()) { _=reader.GetFieldValue<byte[]>(0).Length; count++; }
        return count;
    }
    [Benchmark] public async Task<long> SortWindow() => Convert.ToInt64(await sort.ExecuteScalarAsync());
    [Benchmark] public async Task<int> LatestSnapshot() => ((byte[])(await snapshot.ExecuteScalarAsync())!).Length;
    [Benchmark] public async Task<int> History100()
    {
        await using var reader = await history.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync()) { _ = reader.GetFieldValue<byte[]>(1).Length; count++; }
        return count;
    }
    /// <summary>Eight independent streams/connections concurrently commit one event each; one benchmark operation is the complete group.</summary>
    [Benchmark] public async Task ConcurrentAppend8()
        => await Task.WhenAll(concurrentCommands.Select(ExecuteAppend));

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var command in concurrentCommands) if (command is not null) await command.DisposeAsync();
        foreach (var c in concurrentConnections) if (c is not null) await c.DisposeAsync();
        if (append is not null) await append.DisposeAsync();
        if (batch is not null) await batch.DisposeAsync();
        if (batch8 is not null) await batch8.DisposeAsync();
        if (batch128 is not null) await batch128.DisposeAsync();
        if (workingSet is not null) await workingSet.DisposeAsync();
        if (sort is not null) await sort.DisposeAsync();
        if (snapshot is not null) await snapshot.DisposeAsync();
        if (history is not null) await history.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        if (source is not null) await source.DisposeAsync();
    }
}



