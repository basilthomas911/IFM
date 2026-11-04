using Npgsql;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

/// <summary>Checks real-store creation identity, ordered replay and a stable append boundary.</summary>
public sealed class CreationSnapshotReplayTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Repeated_start_does_not_replace_the_first_committed_creation_fact()
    {
        var name = $"CreationReplay.Start.{Guid.NewGuid():N}";
        var first = Started();
        var later = Started();
        await fixture.ActorEventDb.SaveEventsAsync(name, first.CommandId, new DomainEventCollection([first]), 0, CancellationToken.None);
        await fixture.ActorEventDb.SaveEventsAsync(name, later.CommandId, new DomainEventCollection([later]), 1, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        var rows = new List<EventStreamReadModel>();

        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(
            stream, batch => rows.AddRange(batch));

        Assert.Equal(new long[] { 1, 2 }, rows.Select(row => row.StreamVersion));
        Assert.Equal(first.Id, rows[0].ToDomainEvent().Id);
        Assert.Equal(later.Id, rows[1].ToDomainEvent().Id);
    }

    [Fact]
    public async Task Append_during_reduction_is_excluded_from_the_captured_head()
    {
        var name = $"CreationReplay.Concurrent.{Guid.NewGuid():N}";
        var first = Started();
        await fixture.ActorEventDb.SaveEventsAsync(name, first.CommandId, new DomainEventCollection([first]), 0, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        var rows = new List<EventStreamReadModel>();
        var appended = false;
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(stream, batch =>
        {
            if (!appended)
            {
                appended = true;
                var later = Started();
                Task.Run(async () => await fixture.ActorEventDb.SaveEventsAsync(name, later.CommandId,
                    new DomainEventCollection([later]), 1, CancellationToken.None)).GetAwaiter().GetResult();
            }
            rows.AddRange(batch);
        });
        Assert.Single(rows);
        Assert.Equal(1, rows[0].StreamVersion);
        Assert.Equal(2, await fixture.ActorEventDb.GetCommittedStreamVersionAsync(stream));
    }

    [Fact]
    public async Task Missing_creation_of_a_nonempty_stream_is_rejected()
    {
        var name = $"CreationReplay.Missing.{Guid.NewGuid():N}";
        var first = Started();
        await fixture.ActorEventDb.SaveEventsAsync(name, first.CommandId, new DomainEventCollection([first]), 0, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var remove = new NpgsqlCommand("DELETE FROM event_log WHERE EventStreamId=$1", connection);
        remove.Parameters.AddWithValue(stream);
        await remove.ExecuteNonQueryAsync();

        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(stream, _ => { }));
        Assert.Contains("Creation snapshot", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uncreated_stream_is_empty_and_cancelled_read_does_not_reduce()
    {
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync($"CreationReplay.Empty.{Guid.NewGuid():N}", CancellationToken.None);
        var count = 0;
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(stream,
            rows => count += rows.Count());
        Assert.Equal(0, count);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(stream,
                _ => throw new InvalidOperationException("A cancelled read must not reduce."), cancellation.Token));
    }

    [Fact]
    public async Task Explicit_checkpoint_keeps_creation_and_latest_checkpoint_then_tail()
    {
        var name = $"CreationReplay.Checkpoint.{Guid.NewGuid():N}";
        var facts = Enumerable.Range(0, 6).Select(_ => Started()).ToArray();
        await fixture.ActorEventDb.SaveEventsAsync(name, Guid.NewGuid(), new DomainEventCollection(facts), 0, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        var rows = new List<EventStreamReadModel>();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationWithCheckpointAsync<FuturesRsiSignalCommandState, FuturesRsiSignalStartedEvent>(
            stream, batch => rows.AddRange(batch));
        Assert.Equal(new long[] { 1, 6 }, rows.Select(row => row.StreamVersion));
        Assert.Equal(facts[0].Id, rows[0].ToDomainEvent().Id);
        Assert.Equal(facts[5].Id, rows[1].ToDomainEvent().Id);
        rows.Clear();
        await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationWithCheckpointAsync<FuturesRsiSignalCommandState, FuturesRsiSignalGeneratedEvent>(
            stream, batch => rows.AddRange(batch));
        Assert.Equal(Enumerable.Range(1, 6).Select(value => (long)value), rows.Select(row => row.StreamVersion));
    }

    [Fact]
    public async Task Missing_middle_fact_is_rejected_even_if_reducer_stops_after_creation()
    {
        var name = $"CreationReplay.Gap.{Guid.NewGuid():N}";
        await fixture.ActorEventDb.SaveEventsAsync(name, Guid.NewGuid(), new DomainEventCollection([Started(), Started(), Started()]), 0, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var remove = new NpgsqlCommand("DELETE FROM event_log WHERE EventStreamId=$1 AND StreamVersion=2", connection);
        remove.Parameters.AddWithValue(stream);
        Assert.Equal(1, await remove.ExecuteNonQueryAsync());
        var error = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await fixture.ActorEventDb.MapReduceActorEventStreamFromCreationAsync<FuturesRsiSignalCommandState>(stream, rows => Assert.Single(rows.Take(1))));
        Assert.Contains("replay gap", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stateless_stream_reads_only_creation_and_head_and_restores_the_append_version()
    {
        var name = $"CreationReplay.Metadata.{Guid.NewGuid():N}";
        var facts = Enumerable.Range(0, 64).Select(_ => new TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.FuturesTickDataStreamingStoppedEvent()).ToArray();
        await fixture.ActorEventDb.SaveEventsAsync(name, Guid.NewGuid(), new DomainEventCollection(facts), 0, CancellationToken.None);
        var stream = await fixture.ActorEventDb.GetEventStreamIdAsync(name, CancellationToken.None);
        var state = new TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State.FuturesTickDataCommandState();
        var count = 0;
        await fixture.ActorEventDb.MapReduceStatelessActorEventStreamFromCreationAsync<TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State.FuturesTickDataCommandState>(stream, rows =>
        {
            state.ReplayEvents(rows.Select(row => { count++; return row; }));
        });
        Assert.Equal(2, count);
        Assert.Equal(64, state.CommittedStreamVersion);
        Assert.Empty(state.Events);
    }

    static FuturesRsiSignalStartedEvent Started() => new()
    {
        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
        Subject = new ActorSubject(ActorType.Event, "FuturesRsiSignalEvent", "Started", "ESZ6"),
        EntityId = new FuturesRsiSignalEntityId("ESZ6", new DateOnly(2026, 11, 3), TimeFrameType.Daily, 14),
        StartedOn = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc), StartedBy = "creation-replay-test"
    };
}
