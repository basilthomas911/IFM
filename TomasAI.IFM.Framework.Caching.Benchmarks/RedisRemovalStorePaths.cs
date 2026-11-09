using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventProjector.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Caching.Benchmarks;

internal sealed partial class RedisRemovalFixture
{
    /// <summary>Measures unchanged single-query reads and real durable recovery markers, never Development data.</summary>
    public async Task MeasureStorePathsAsync()
    {
        const string stream = "RedisRemoval.StorePaths";
        const string sidecar = stream + ".checkpoint";
        FuturesRsiSignalGeneratedEvent Fact() => new()
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), Subject = ActorSubject.Unknown,
            EntityId = new("ES20261218", new DateOnly(2026, 11, 9), TimeFrameType.Daily, 14),
            CreatedOn = DateTime.UtcNow, ReceivedOn = DateTime.UtcNow,
            CreatedBy = "isolated-removal-benchmark", EventSource = "RedisRemovalBenchmark"
        };
        var commandId = Guid.NewGuid();
        var source = await Events.SaveEventsAsync(stream, commandId,
            new DomainEventCollection(Enumerable.Range(0, 512).Select(_ => (IEvent)Fact()).ToArray()),
            0, CancellationToken.None);
        if (!await Events.SaveAggregateCheckpointAsync(stream, sidecar, 511, Fact(), CancellationToken.None))
            throw new InvalidOperationException("Checkpoint seed failed.");
        var expected = JsonSerializer.Serialize(await Events.ReadAggregateCheckpointWindowAsync(stream, sidecar, CancellationToken.None));
        var states = source.Take(32).Select(e => new EventProjectorStateReadModel(e.EventId,
            "RedisRemovalBenchmark", "RedisRemovalProjector", true, 1,
            EventProjectorOutcomeType.Retrying, EventProjectorStageType.ApplyProjection)).ToArray();
        foreach (var state in states) await Events.InsertEventProjectorStateAsync(state);
        var results = new List<object>();
        // Alternate the order of each pair to avoid attributing ambient database
        // load changes to an unchanged single-query implementation.
        await Measure("LatestSnapshotAndSuffixSingleQuery", 200, async before =>
        {
            var db = before ? raw : Events;
            var window = await db.ReadAggregateCheckpointWindowAsync(stream, sidecar, CancellationToken.None);
            if (JsonSerializer.Serialize(window) != expected) throw new InvalidOperationException("Snapshot mismatch.");
        });
        await Measure("CommandOutcomeSingleQuery", 200, async before =>
        {
            var rows = await (before ? raw : Events).FindAggregateEventsAsync(stream, commandId, CancellationToken.None);
            if (rows.Count == 0) throw new InvalidOperationException("Command evidence missing.");
        });
        await Measure("DurableRecoveryMarkerBatch32", 40, async before =>
        {
            var db = before ? raw : Events;
            foreach (var seed in states)
            {
                var state = await db.GetEventProjectorStateAsync(seed.EventId, seed.ProjectorName)
                    ?? throw new InvalidOperationException("Recovery marker missing.");
                await db.InsertEventProjectorStateAsync(state);
                if (before) Mirror(state);
            }
        });

        async Task Measure(string workload, int count, Func<bool, Task> operation)
        {
            for (var warmup = 0; warmup < 10; warmup++)
            {
                await operation(true); await operation(false);
            }
            var elapsed = new[] { new List<double>(), new List<double>() };
            var allocated = new long[2];
            var collections = new[] { new int[3], new int[3] };
            for (var sample = 0; sample < count; sample++)
                foreach (var index in sample % 2 == 0 ? new[] { 0, 1 } : new[] { 1, 0 })
                {
                    var bytes = GC.GetTotalAllocatedBytes(false);
                    var generations = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                    var started = Stopwatch.GetTimestamp();
                    await operation(index == 0);
                    elapsed[index].Add(Stopwatch.GetElapsedTime(started).TotalMicroseconds);
                    allocated[index] += GC.GetTotalAllocatedBytes(false) - bytes;
                    for (var generation = 0; generation < 3; generation++)
                        collections[index][generation] += GC.CollectionCount(generation) - generations[generation];
                }
            for (var index = 0; index < 2; index++)
            {
                elapsed[index].Sort();
                results.Add(new { Path = index == 0 ? "RedisBefore" : "LocalAfter", Workload = workload,
                    Samples = count, MeanUs = elapsed[index].Average(), P50Us = elapsed[index][count / 2],
                    P95Us = elapsed[index][(int)(count * .95)], AllocatedBytesPerSample = allocated[index] / count,
                    GenCollections = collections[index] });
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
