using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSessionBarSignal;

public sealed class BarBatchRecoveryTests
{
    static string Root() => Path.Combine(Path.GetTempPath(), "IFM-bar-batch-test-" + Guid.NewGuid().ToString("N"));
    static IEvent[] Trades(int count) => Enumerable.Range(1, count).Select(n => (IEvent)RealtimeMailboxSafetyFixture.Trade(n)).ToArray();

    [Fact]
    public async Task Failed_publication_after_batch_commit_restores_every_input_and_retries_without_duplicate_bars()
    {
        var root = Root(); var original = new RealtimeMailboxSafetyFixture.Handler();
        foreach (var trade in Trades(64)) await original.Receive((TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent)trade);
        await Close(original);
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var before = new RealtimeMailboxSafetyFixture.Handler(store) { Fail = true };
            await Assert.ThrowsAsync<IOException>(() => before.ReceiveBatch(Trades(64)).AsTask());
            Assert.Empty(before.Persisted);
        }
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        {
            var restored = new RealtimeMailboxSafetyFixture.Handler(store);
            await restored.ReceiveBatch(Trades(64)); await restored.ReceiveBatch(Trades(64));
            await Close(restored);
            Equal(original.Persisted, restored.Persisted);
        }
    }

    [Fact]
    public async Task Acknowledged_batch_open_state_survives_restart()
    {
        var root = Root(); var accepted = new List<FuturesTradeSessionBarReadModel>();
        using (var store = new FuturesTradeSessionBarCheckpointStore(root))
        { var before = new RealtimeMailboxSafetyFixture.Handler(store, accepted); await before.ReceiveBatch(Trades(64)); }
        using var recovery = new FuturesTradeSessionBarCheckpointStore(root);
        var after = new RealtimeMailboxSafetyFixture.Handler(recovery, accepted);
        await Close(after);
        var daily = Assert.Single(accepted, bar => bar.TimeFrame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
        Assert.Equal(64, daily.TradeCount); Assert.Equal(101m, daily.Open); Assert.Equal(164m, daily.Close);
        Assert.Equal(64, daily.LastSourceSequence);
    }

    [Fact]
    public async Task Barriers_remain_at_their_original_FIFO_position_in_the_batch()
    {
        var serial = new RealtimeMailboxSafetyFixture.Handler(); var batched = new RealtimeMailboxSafetyFixture.Handler();
        var first = RealtimeMailboxSafetyFixture.Trade(1); var second = RealtimeMailboxSafetyFixture.Trade(2);
        var id = serial.Context.Accumulators.Resolve(first.EntityId.ContractId, first.EntityId.ValueDate);
        var barrier = new FuturesTradeSessionBarSignalBarrierRealtimeEvent
        { EntityId = id, Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName, "Barrier", id.Format()),
            BarrierUtc = first.Price.Trade!.Value.EventTimestamp.AddSeconds(16) };
        await serial.Receive(first); await barrier.ExecuteAsync(serial.Context, NullLogger.Instance); await serial.Receive(second);
        await batched.ReceiveBatch([first, barrier, second]);
        await Close(serial); await Close(batched);
        Equal(serial.Persisted, batched.Persisted);
    }

    [Fact]
    public async Task Cancelled_batch_is_not_accepted_and_can_be_retried()
    {
        using var store = new FuturesTradeSessionBarCheckpointStore(Root());
        var handler = new RealtimeMailboxSafetyFixture.Handler(store);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.ReceiveBatch(Trades(64), new CancellationToken(true)).AsTask());
        Assert.Empty(handler.Persisted);
        await handler.ReceiveBatch(Trades(64)); await Close(handler);
        Assert.Equal(64, Assert.Single(handler.Persisted, bar => bar.TimeFrame == TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily).TradeCount);
    }

    static Task Close(RealtimeMailboxSafetyFixture.Handler handler)
    {
        var id = handler.Context.Accumulators.Resolve("ESU6", RealtimeMailboxSafetyFixture.ValueDate);
        return handler.ReceiveBatch([new FuturesTradeSessionBarSignalBarrierRealtimeEvent
        { EntityId = id, Subject = new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime, FuturesTradeSessionBarSignalRealtimeActor.ActorName, "Barrier", id.Format()),
            BarrierUtc = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero) }]).AsTask();
    }
    static void Equal(IReadOnlyList<FuturesTradeSessionBarReadModel> expected, IReadOnlyList<FuturesTradeSessionBarReadModel> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) Assert.Equal(MessagePackSerializer.Serialize(expected[i]), MessagePackSerializer.Serialize(actual[i]));
    }
}
