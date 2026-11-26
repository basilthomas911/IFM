using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

public sealed class BarCheckpointRecoveryTests
{
    static string Root() => Path.Combine(Path.GetTempPath(), "IFM-bar-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Restored_gap_buffer_preserves_exact_bars_and_source_lineage()
    {
        var uninterrupted = new RealtimeMailboxSafetyFixture.Handler();
        await uninterrupted.Receive(RealtimeMailboxSafetyFixture.Trade(1));
        await uninterrupted.Receive(RealtimeMailboxSafetyFixture.Trade(3));
        await uninterrupted.Receive(RealtimeMailboxSafetyFixture.Trade(2));
        var root = Root();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store);
            await before.Receive(RealtimeMailboxSafetyFixture.Trade(1));
            await before.Receive(RealtimeMailboxSafetyFixture.Trade(3));
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var after = new RealtimeMailboxSafetyFixture.Handler(store);
            await after.Receive(RealtimeMailboxSafetyFixture.Trade(2));
            Assert.Single(after.Persisted);
            Assert.Equal(MessagePackSerializer.Serialize(uninterrupted.Persisted[0]), MessagePackSerializer.Serialize(after.Persisted[0]));
            await Close(after);
            await Close(uninterrupted);
            Assert.Equal(uninterrupted.Persisted.Count, after.Persisted.Count);
            for (var i = 0; i < after.Persisted.Count; i++)
                Assert.Equal(MessagePackSerializer.Serialize(uninterrupted.Persisted[i]), MessagePackSerializer.Serialize(after.Persisted[i]));
        }
    }

    [Fact]
    public async Task Barrier_pending_bars_survive_restart_and_retry_in_order()
    {
        var root = Root();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store);
            await before.Receive(RealtimeMailboxSafetyFixture.Trade(1)); before.Throw = true;
            await Assert.ThrowsAsync<IOException>(() => Close(before));
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var after = new RealtimeMailboxSafetyFixture.Handler(store);
            // The old session remains eligible for recovery even well after the one-minute close window.
            Assert.Single(after.Context.Accumulators.ActivePartitions(DateTimeOffset.UtcNow));
            await Close(after); await Close(after);
            Assert.Equal(7, after.Persisted.Count);
            Assert.Equal(7, after.Persisted.Select(bar => bar.ObservationId).Distinct().Count());
            Assert.All(after.Persisted, bar => Assert.Equal(RealtimeMailboxSafetyFixture.Trade(1).Price.Trade!.Value.EventTimestamp, bar.LastMarketEventUtc));
        }
    }

    [Fact]
    public async Task Corrupt_checkpoint_blocks_recovery_instead_of_starting_empty()
    {
        var root = Root();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        { var handler = new RealtimeMailboxSafetyFixture.Handler(store); await handler.Receive(RealtimeMailboxSafetyFixture.Trade(1)); }
        var path = Directory.GetFiles(root, "*.bars").Single();
        var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
        using var recovery = new FuturesTradeSessionBarCheckpointStore(root);
        Assert.Throws<InvalidDataException>(() => new RealtimeMailboxSafetyFixture.Handler(recovery));
    }

    [Fact]
    public async Task Checkpoint_write_failure_rolls_back_before_redelivery()
    {
        var root = Root();
        using var store = new FuturesTradeSessionBarCheckpointStore(root);
        var handler = new RealtimeMailboxSafetyFixture.Handler(store);
        await handler.Receive(RealtimeMailboxSafetyFixture.Trade(1));
        var path = Directory.GetFiles(root, "*.bars").Single();
        Assert.Single(store.Partitions()); // Recovery closes the cached writer before fault injection.
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => handler.Receive(RealtimeMailboxSafetyFixture.Trade(2)).AsTask());
        Assert.Empty(handler.Persisted);
        await handler.Receive(RealtimeMailboxSafetyFixture.Trade(2));
        Assert.Single(handler.Persisted);
        Assert.Equal(101m, handler.Persisted[0].Open);
    }

    [Fact]
    public void Recovery_root_has_one_writer()
    {
        var root = Root(); using var first = new FuturesTradeSessionBarCheckpointStore(root);
        Assert.Throws<IOException>(() => new FuturesTradeSessionBarCheckpointStore(root));
    }

    [Fact]
    public async Task Incomplete_unacknowledged_tail_is_removed_before_resuming()
    {
        var root = Root();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        { var handler = new RealtimeMailboxSafetyFixture.Handler(store); await handler.Receive(RealtimeMailboxSafetyFixture.Trade(1)); }
        var path = Directory.GetFiles(root, "*.bars").Single();
        var committedLength = new FileInfo(path).Length;
        using (var file = new FileStream(path, FileMode.Append, FileAccess.Write)) file.Write(new byte[7]);
        using var recoveredStore = new FuturesTradeSessionBarCheckpointStore(root);
        var recovered = new RealtimeMailboxSafetyFixture.Handler(recoveredStore);
        Assert.Equal(committedLength, new FileInfo(path).Length);
        await recovered.Receive(RealtimeMailboxSafetyFixture.Trade(2));
        Assert.Single(recovered.Persisted);
    }

    [Fact]
    public async Task Partially_published_barrier_does_not_repeat_committed_prefix()
    {
        var root = Root(); var committed = new List<FuturesTradeSessionBarReadModel>();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store, committed) { RejectAfterSuccesses = 3 };
            await before.Receive(RealtimeMailboxSafetyFixture.Trade(1));
            await Assert.ThrowsAsync<IOException>(() => Close(before));
            Assert.Equal(3, committed.Count);
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var after = new RealtimeMailboxSafetyFixture.Handler(store, committed);
            await Close(after);
            Assert.Equal(4, after.Attempts.Count);
            Assert.Equal(7, committed.Count);
        }
    }

    [Fact]
    public async Task Retired_epoch_cannot_return_after_restart()
    {
        var root = Root(); var first = RealtimeMailboxSafetyFixture.Trade(1);
        var next = RealtimeMailboxSafetyFixture.Trade(2);
        var epoch = Guid.NewGuid();
        next = next with { Price = next.Price with { Trade = next.Price.Trade!.Value with { StreamEpochId = epoch } } };
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store);
            await before.Receive(first); await before.Receive(next);
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var after = new RealtimeMailboxSafetyFixture.Handler(store);
            await after.Receive(RealtimeMailboxSafetyFixture.Trade(3));
            await Close(after);
            Assert.Equal(7, after.Persisted.Count);
            Assert.All(after.Persisted, bar => { Assert.Equal(epoch, bar.StreamEpochId); Assert.Equal(1, bar.TradeCount); });
        }
    }

    [Fact]
    public async Task Journal_compaction_preserves_accumulated_OHLCV()
    {
        var root = Root(); var committed = new List<FuturesTradeSessionBarReadModel>();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store, committed);
            for (var ordinal = 1; ordinal <= 2500; ordinal++) await before.Receive(RealtimeMailboxSafetyFixture.Trade(ordinal));
        }
        var path = Directory.GetFiles(root, "*.bars").Single();
        Assert.InRange(new FileInfo(path).Length, 1, 4 * 1024 * 1024);
        using var recoveredStore = new FuturesTradeSessionBarCheckpointStore(root);
        var recovered = new RealtimeMailboxSafetyFixture.Handler(recoveredStore, committed);
        await Close(recovered);
        var daily = Assert.Single(committed, bar => bar.TimeFrame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
        Assert.Equal(2500, daily.TradeCount);
        Assert.Equal(2500m, daily.Volume);
        Assert.Equal(101m, daily.Open);
        Assert.Equal(2600m, daily.High);
        Assert.Equal(101m, daily.Low);
        Assert.Equal(2600m, daily.Close);
        Assert.Equal(2500, daily.LastSourceSequence);
    }

    [Fact]
    public async Task Actor_failure_escapes_even_without_mailbox_metrics()
    {
        var handler = new RealtimeMailboxSafetyFixture.Handler();
        handler.Context.Logger.Returns(NullLogger<FuturesTradeSessionBarSignalRealtimeActor>.Instance);
        handler.Context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName));
        var actor = new FuturesTradeSessionBarSignalRealtimeActor(handler.Context);
        await actor.HandleMessageAsync(Message(RealtimeMailboxSafetyFixture.Trade(1)));
        handler.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => actor.HandleMessageAsync(Message(RealtimeMailboxSafetyFixture.Trade(2))).AsTask());
        handler.Fail = false;
        await actor.HandleMessageAsync(Message(RealtimeMailboxSafetyFixture.Trade(2)));
        Assert.Single(handler.Persisted);
    }

    static IActorMessage Message(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(new ActorSubject(ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName,
            source.Subject.Verb, source.EntityId.Format()));
        message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(source);
        return message;
    }
    static Task Close(RealtimeMailboxSafetyFixture.Handler handler)
    {
        var id = handler.Context.Accumulators.Resolve("ESU6", RealtimeMailboxSafetyFixture.ValueDate);
        return new FuturesTradeSessionBarSignalBarrierRealtimeEvent
        { EntityId = id, BarrierUtc = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero) }
            .ExecuteAsync(handler.Context, NullLogger.Instance).AsTask();
    }
}
