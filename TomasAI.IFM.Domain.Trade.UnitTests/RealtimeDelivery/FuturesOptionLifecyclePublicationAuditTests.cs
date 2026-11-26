using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

/// <summary>Durable lifecycle ownership survives notification failure after local state mutation.</summary>
public sealed class FuturesOptionLifecyclePublicationAuditTests
{
    [Fact]
    public async Task Qualified_retry_keeps_legacy_zero_id_lifecycle_request_and_frozen_output_unchanged()
    {
        var context=Substitute.For<IFuturesOptionRealtimeContext>();var caches=new TradeRealtimeCacheRegistry();
        await using var store=new PositionRealtimeIntents();context.PositionIntents.Returns(store);context.RouteIndex.Returns(new ContractIdRouteIndex());context.RealtimeCache.Returns(caches);
        var position=IronCondorPositionDeliveryFixture.Position(1) with{RouteGeneration=1};
        var cache=caches.Register(new EstablishedTradeDefinition{Id=position.Id.Trade,StrategyKind=TradeStrategyKind.IronCondor},position.ValueDate);
        var source=new OpenPositionRoutesChangedEvent{Id=Guid.Empty,EntityId=position.Id,Position=position};
        var request=MessagePack.MessagePackSerializer.Serialize(source);
        var output=MessagePack.MessagePackSerializer.Serialize(TradeRealtimeSnapshotChangedEvent.From(cache.PreparePositionPublication(position)));
        var key=TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.TradePlanContractIdentity.DeterministicId($"option-position-lifecycle/v1|{position.Id.Format()}|{position.RouteGeneration}|{position.PositionSequence}");
        var hash=PositionRealtimeIntents.Hash(position);
        await store.CaptureAsync(key,hash,()=>new(){Kind=PositionIntentKind.Lifecycle,PositionId=position.Id,RouteGeneration=position.RouteGeneration,
            PositionSequence=position.PositionSequence,Request=request,Output=output,Flags=PositionIntentFlags.OutputCaptured});
        byte[]? sent=null;context.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(Arg.Any<TradeRealtimeSnapshotChangedEvent>()).Returns(call=>{sent=MessagePack.MessagePackSerializer.Serialize(call.Arg<TradeRealtimeSnapshotChangedEvent>());return ValueTask.CompletedTask;});
        await (source with{Id=Guid.NewGuid()}).ExecuteAsync(context);
        var retained=await store.FindAsync(key,hash);Assert.True(retained!.IsCompleted);Assert.Equal(request,retained.Request);Assert.Equal(output,retained.Output);Assert.Equal(output,sent);
        Assert.Equal(Guid.Empty,MessagePack.MessagePackSerializer.Deserialize<OpenPositionRoutesChangedEvent>(retained.Request).Id);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Failed_lifecycle_snapshot_retries_identically_and_cleanup_completes(bool closing)
    {
        var context=Substitute.For<IFuturesOptionRealtimeContext>();var routes=new ContractIdRouteIndex();var caches=new TradeRealtimeCacheRegistry();
        await using var intents=new PositionRealtimeIntents();context.PositionIntents.Returns(intents);
        context.RouteIndex.Returns(routes);context.RealtimeCache.Returns(caches);
        var open=IronCondorPositionDeliveryFixture.Position(1) with{RouteGeneration=1};
        var cache=caches.Register(new EstablishedTradeDefinition{Id=open.Id.Trade,StrategyKind=TradeStrategyKind.IronCondor},open.ValueDate);
        if(closing)
        {
            Assert.True(cache.Apply(open));
            Assert.True(routes.Add(new PortfolioFundTradeLeg(open.Id.Trade.PortfolioId,open.Id.Trade.FundId,open.Id.Trade.OrderId,open.Id.Trade.TradeId,
                open.Id.PositionId,Guid.NewGuid(),TradeStrategyKind.IronCondor,1),"ES.TEST.CALL"));
        }
        var position=closing?open with{IsOpen=false,RouteGeneration=2,PositionSequence=2}:open;
        var notifications=new List<byte[]>();
        var source=new OpenPositionRoutesChangedEvent{Id=Guid.NewGuid(),EntityId=position.Id,Position=position};var attempts=0;
        context.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(Arg.Any<TradeRealtimeSnapshotChangedEvent>())
            .Returns(call=>{notifications.Add(MessagePack.MessagePackSerializer.Serialize(call.Arg<TradeRealtimeSnapshotChangedEvent>()));attempts++;return attempts==1?ValueTask.FromException(new IOException("Lifecycle snapshot was not accepted.")):ValueTask.CompletedTask;});
        await Assert.ThrowsAsync<IOException>(async()=>await source.ExecuteAsync(context));Assert.Equal(1,attempts);
        await source.ExecuteAsync(context);
        Assert.Equal(notifications[0],notifications[1]);Assert.Equal(2,attempts);Assert.Equal(!closing,caches.Find(open.Id.Trade) is not null);
        Assert.Equal(!closing,cache.Read().Position!.IsOpen);
    }
}
