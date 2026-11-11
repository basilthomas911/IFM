using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures committed stream resolution and latest-snapshot selection through the real context.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class EventSourceContextConventionBenchmarks
{
    EventSourceActorDbContext database = null!;
    string stream = null!;

    [Params(false, true)] public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var raw = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(raw))
        {
            await guard.OpenAsync();
            await using var durabilityCommand = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await durabilityCommand.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        builder.Pooling = !ColdConnection;
        builder.Username = "";
        builder.Password = "";
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
        var factory = new DbContextFactory(new DbContextResolver(_ => database));
        database = new(settings, factory, logger);
        stream = "benchmark/context/snapshot/" + Guid.NewGuid().ToString("N");
        var id = new FuturesRsiSignalEntityId("ES20261218", new DateOnly(2026, 11, 10), TimeFrameType.Daily, 14);
        var command = Guid.NewGuid();
        var events = Enumerable.Range(1, 512).Select(index => index is 1 or 489
            ? (IEvent)new FuturesRsiSignalStartedEvent { EntityId = id, Id = Guid.NewGuid(), CommandId = command }
            : new FuturesRsiSignalStoppedEvent { EntityId = id, Id = Guid.NewGuid(), CommandId = command }).ToArray();
        await database.SaveEventsAsync(stream, command, new DomainEventCollection(events), 0, CancellationToken.None);
        if (await ReadVersionWithResolution() != 512 || await ReadSnapshotWithResolution() != 24)
            throw new InvalidOperationException("The latest snapshot qualification fixture did not preserve its exact event window.");
    }

    [Benchmark] public async Task<long> ReadVersionWithResolution()
    {
        var identity = await database.GetEventStreamIdFromDbAsync(stream).ConfigureAwait(false);
        return await database.GetCommittedStreamVersionAsync(identity!.EventStreamId).ConfigureAwait(false);
    }

    [Benchmark] public async Task<int> ReadSnapshotWithResolution()
    {
        var identity = await database.GetEventStreamIdFromDbAsync(stream).ConfigureAwait(false);
        var rows = await database.GetEventsFromSnapshotAsync<FuturesRsiSignalStartedEvent>(identity!.EventStreamId).ConfigureAwait(false);
        return rows.Count;
    }
}
