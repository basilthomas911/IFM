using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Measures the same committed source rows before and after creation-based loading.</summary>
public sealed class CreationSnapshotLoadTimingTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(100_000)]
    [InlineData(1_200_000)]
    public async Task Creation_replay_load_timings(int eventCount)
    {
        var database = fixture.ActorEventDb;
        var streamName = $"CreationReplay.Timing.{Guid.NewGuid():N}";
        var entity = new FuturesRsiSignalEntityId("ESZ6", new DateOnly(2026, 11, 3), TimeFrameType.Daily, 14);
        var created = new FuturesRsiSignalStartedEvent { EntityId = entity, Id = Guid.NewGuid(), CommandId = Guid.NewGuid() };
        await database.SaveEventsAsync(streamName, created.CommandId, new DomainEventCollection([created]), 0, CancellationToken.None);
        var streamId = await database.GetEventStreamIdAsync(streamName, CancellationToken.None);
        var generated = new FuturesRsiSignalStoppedEvent { EntityId = entity, Id = Guid.NewGuid() };
        var payload = EventLogMessagePackCodec.Shared.Serialize(generated);

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        // Dense payload hydration is measured independently of indicator calculation.
        // Repeated stop facts are valid lifecycle facts and keep the output state stable.
        await using (var insert = new NpgsqlCommand("""
            WITH event_name AS (
                INSERT INTO event_name_id (EventName, EventTypeName) VALUES ($2,$3)
                ON CONFLICT (EventName,EventTypeName) DO UPDATE SET EventName=EXCLUDED.EventName
                RETURNING EventNameId
            ), inserted AS (
                INSERT INTO event_log (EventStreamId,EventNameId,StreamVersion,EventPayload,CommandId,EventTimestamp)
                SELECT $1,EventNameId,n,$4,$5,'2026-11-03T12:00:00Z'
                FROM generate_series(2,$6::integer) n CROSS JOIN event_name
                RETURNING EventStreamId
            )
            UPDATE event_stream_id SET CurrentVersion=$6 WHERE EventStreamId=$1;
            """, connection))
        {
            insert.CommandTimeout = 180;
            insert.Parameters.AddWithValue(streamId);
            insert.Parameters.AddWithValue(generated.EventName);
            insert.Parameters.AddWithValue(typeof(FuturesRsiSignalStoppedEvent).AssemblyQualifiedName!);
            insert.Parameters.AddWithValue(payload);
            insert.Parameters.AddWithValue(Guid.NewGuid());
            insert.Parameters.AddWithValue(eventCount);
            await insert.ExecuteNonQueryAsync();
        }

        var requestedVariant = Environment.GetEnvironmentVariable("IFM_CREATION_REPLAY_VARIANT") ?? "before";
        var variants = requestedVariant == "compare" ? new[] { "before", "after" } : new[] { requestedVariant };
        foreach (var variant in variants)
        {
            var samples = new List<double>();
            var allocations = new List<long>();
            for (var iteration = 0; iteration < 6; iteration++)
            {
                var count = 0L;
                var state = new FuturesRsiSignalCommandState();
                void Replay(IEnumerable<EventStreamReadModel> rows)
                {
                    state.ReplayEvents(rows.Select(row => { count++; return row; }));
                }
                var allocated = GC.GetTotalAllocatedBytes(true);
                var stopwatch = Stopwatch.StartNew();
                if (variant == "before")
                {
                    _ = await database.GetCommittedStreamVersionAsync(streamId, CancellationToken.None);
                    await database.MapReduceActorEventStreamAsync<FuturesRsiSignalCommandState>(streamId, Replay, CancellationToken.None);
                }
                else
                {
                    // Reflection permits building/running the baseline against the unchanged API.
                    var method = database.GetType().GetMethods().Single(method =>
                        method.Name == "MapReduceActorEventStreamFromCreationAsync" && method.IsGenericMethodDefinition);
                    await (ValueTask)method.MakeGenericMethod(typeof(FuturesRsiSignalCommandState))
                        .Invoke(database, [streamId, (Action<IEnumerable<EventStreamReadModel>>)Replay, CancellationToken.None])!;
                }
                stopwatch.Stop();
                Assert.Equal(eventCount, count);
                Assert.Equal(eventCount, state.CommittedStreamVersion);
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                allocations.Add(GC.GetTotalAllocatedBytes(true) - allocated);
            }
            var warm = samples.Skip(1).Order().ToArray();
            var result = new { Variant = variant, Events = eventCount, FirstLoadMs = samples[0],
                WarmP50Ms = warm[warm.Length / 2], WarmP95Ms = warm[^1], AllocatedBytes = allocations };
            var json = JsonSerializer.Serialize(result);
            output.WriteLine(json);
            var path = Environment.GetEnvironmentVariable("IFM_CREATION_REPLAY_TIMINGS");
            if (path is not null) await File.AppendAllTextAsync(path, json + Environment.NewLine);
        }
    }
}
