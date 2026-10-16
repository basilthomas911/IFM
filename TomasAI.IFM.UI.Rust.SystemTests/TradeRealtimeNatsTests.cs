using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class TradeRealtimeNatsTests
{
    [TradeCacheNatsFact]
    public async Task Whole_snapshot_crosses_real_actor_transport_and_encodes_for_native_display()
    {
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = Enumerable.Range(0, 4).Select(i => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = $"option-{i}" }).ToArray() };
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var source = new TradeRealtimeHotCache(trade, date);
        var mirror = new TradeRealtimeHotCache(trade, date);
        var actor = "TradeCacheVerification" + Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<TradeRealtimeSnapshotChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caughtUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var listener = new NatsActorEventListener(new NatsEventListenerOptions(), NullLogger.Instance);
        await listener.StartAsync(actor, new() { [new(ActorType.Notify, actor)] = [TradeRealtimeSnapshotChangedEvent.Verb] }, (_, message) =>
        {
            var update = message.AsEvent<TradeRealtimeSnapshotChangedEvent>()!;
            mirror.AcceptSnapshot(update.Snapshot);
            received.TrySetResult(update);
            if (update.Snapshot.SnapshotRevision == 200) caughtUp.TrySetResult();
            return ValueTask.CompletedTask;
        });
        await using var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Notify, actor + "Producer"));
        var changed = TradeRealtimeSnapshotChangedEvent.From(source.Read());
        changed = changed with { Subject = new(ActorType.Notify, actor, TradeRealtimeSnapshotChangedEvent.Verb, trade.Id.Format()) };
        await producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(changed.Subject, changed);
        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(delivered.Snapshot.SnapshotRevision, mirror.Read().SnapshotRevision);
        Assert.Equal(4, mirror.Read().Legs.Length);
        Assert.Equal(source.Read().MonitoringGeneration, mirror.Read().MonitoringGeneration);
        Assert.NotEmpty(BridgeDocument.Encode(mirror.Read(), camelCase: true, enumNames: true));
        for (var revision = 1; revision <= 200; revision++)
        {
            source.Apply(new TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel(
                "option-0", date, revision, new(15, 0), revision, revision, revision + 1, 1, 1, .2, 5000, -.4, .01, 12, -3, .1));
            var update = TradeRealtimeSnapshotChangedEvent.From(source.Read());
            update = update with { Subject = changed.Subject };
            await producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(update.Subject, update);
        }
        await caughtUp.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(200, mirror.Read().SnapshotRevision);
        Assert.Equal(200, mirror.Read().Legs[0].Observation!.TickId);
        Assert.Equal(4, mirror.Read().Legs.Length);
    }
}

public sealed class TradeCacheNatsFactAttribute : FactAttribute
{
    public TradeCacheNatsFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_TRADE_CACHE_NATS_TEST") != "1")
            Skip = "Set IFM_TRADE_CACHE_NATS_TEST=1 to verify the configured local NATS transport.";
    }
}
