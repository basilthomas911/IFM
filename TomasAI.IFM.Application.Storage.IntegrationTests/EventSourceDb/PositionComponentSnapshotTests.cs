using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Qualifies latest position/limit snapshots against complete source replay in an isolated PostgreSQL database.</summary>
public sealed class PositionComponentSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Latest_snapshots_preserve_limits_on_either_side_of_the_position_and_capture_one_head(bool limitsLast)
    {
        var position = Position(1);
        var limits = new IronCondorMonitoringInitializedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), SpreadLimits = [new()] };
        IEvent[] facts = limitsLast
            ? [position, Position(2), Position(3), limits]
            : [position, limits, Position(2), Position(3)];
        var stream = await Save(facts);
        var full = new IronCondorPositionCommandState();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<IronCondorPositionCommandState>(stream, full.ReplayEvents);
        var restored = new IronCondorPositionCommandState();
        var rows = new List<EventStreamReadModel>();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromLatestSnapshotsAsync<IronCondorPositionCommandState,
            IronCondorPositionChangedEvent, IronCondorMonitoringInitializedEvent>(stream, batch => rows.AddRange(batch));
        restored.ReplayEvents(rows);
        Assert.Equivalent(full.PositionSnapshot, restored.PositionSnapshot);
        Assert.Equivalent(full.TradeLimits, restored.TradeLimits);
        Assert.Equivalent(full.SpreadLimits, restored.SpreadLimits);
        Assert.Equal(4, restored.CommittedStreamVersion);
        Assert.Equal(limitsLast ? new long[] { 1, 3, 4 } : [1, 2, 4], rows.Select(row => row.StreamVersion));
        Assert.Empty(restored.Events);
    }

    [Fact]
    public async Task Missing_limits_remain_absent_instead_of_inventing_initialization()
    {
        var stream = await Save([Position(1), Position(2)]);
        var state = new IronCondorPositionCommandState();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromLatestSnapshotsAsync<IronCondorPositionCommandState,
            IronCondorPositionChangedEvent, IronCondorMonitoringInitializedEvent>(stream, state.ReplayEvents);
        Assert.Equal(2, state.PositionSnapshot!.PositionSequence);
        Assert.Null(state.TradeLimits);
        Assert.Empty(state.SpreadLimits);
        Assert.Equal(2, state.CommittedStreamVersion);
    }

    [Fact]
    public async Task Dense_history_restores_three_facts_and_matches_full_replay_with_before_after_timings()
    {
        var stream = await Save([Position(1), new IronCondorMonitoringInitializedEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid() }]);
        var fact = Position(100_000);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO event_log(EventStreamId,EventNameId,StreamVersion,EventPayload,CommandId,EventTimestamp)
            SELECT $1,EventNameId,n,$2,$3,'2026-11-03T12:00:00Z'
            FROM generate_series(3,100000) n CROSS JOIN
                (SELECT EventNameId FROM event_name_id WHERE EventName=$4 ORDER BY EventNameId DESC LIMIT 1) names
            """, connection);
        insert.CommandTimeout = 120;
        insert.Parameters.AddWithValue(stream);
        insert.Parameters.AddWithValue(EventLogMessagePackCodec.Shared.Serialize(fact));
        insert.Parameters.AddWithValue(Guid.NewGuid());
        insert.Parameters.AddWithValue(nameof(IronCondorPositionChangedEvent));
        await insert.ExecuteNonQueryAsync();
        await using (var head = new NpgsqlCommand("UPDATE event_stream_id SET CurrentVersion=100000 WHERE EventStreamId=$1", connection))
        {
            head.Parameters.AddWithValue(stream);
            await head.ExecuteNonQueryAsync();
        }
        var measurements = new List<object>();
        IronCondorPositionCommandState? baseline = null;
        foreach (var optimized in new[] { false, true })
        {
            var state = new IronCondorPositionCommandState();
            var count = 0;
            void Replay(IEnumerable<EventStreamReadModel> rows) => state.ReplayEvents(rows.Select(row => { count++; return row; }));
            var watch = Stopwatch.StartNew();
            if (optimized)
                await fixture.ActorEventDb.MapReduceActorEventStreamFromLatestSnapshotsAsync<IronCondorPositionCommandState,
                    IronCondorPositionChangedEvent, IronCondorMonitoringInitializedEvent>(stream, Replay);
            else
                await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<IronCondorPositionCommandState>(stream, Replay);
            measurements.Add(new { optimized, rows = count, milliseconds = watch.Elapsed.TotalMilliseconds });
            Assert.Equal(optimized ? 3 : 100_000, count);
            Assert.Equal(100_000, state.CommittedStreamVersion);
            if (optimized) { Assert.Equivalent(baseline!.Current, state.Current); Assert.Equivalent(baseline.TradeLimits, state.TradeLimits); }
            else baseline = state;
        }
        var json = JsonSerializer.Serialize(measurements);
        output.WriteLine(json);
        if (Environment.GetEnvironmentVariable("IFM_POSITION_REPLAY_TIMINGS") is { } path)
            await File.AppendAllTextAsync(path, json + Environment.NewLine);
    }

    async Task<long> Save(IEvent[] facts)
    {
        var name = $"PositionSnapshots.{Guid.NewGuid():N}";
        await fixture.ActorEventDb.SaveEventsAsync(name, Guid.NewGuid(), new DomainEventCollection(facts), 0, CancellationToken.None);
        return await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
    }

    static IronCondorPositionChangedEvent Position(long sequence) => new()
    {
        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), PositionSnapshot = new StrategyPositionSnapshot { PositionSequence = sequence }
    };
}
