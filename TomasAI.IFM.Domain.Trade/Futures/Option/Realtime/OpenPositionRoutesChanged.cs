using TomasAI.IFM.Shared.EventModelActor;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

public static class OpenPositionRoutesChanged
{

    extension(OpenPositionRoutesChangedEvent changed)
    {
        public async ValueTask ExecuteAsync(IFuturesOptionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            if(context.PositionIntents is {} store)
            {
                await ExecuteDurableAsync(changed,context,store).ConfigureAwait(false);
                return;
            }
            if (!changed.Position.IsOpen)
            {
                if (context.RouteIndex.RemovePosition(changed.Position.Id.PositionId, changed.Position.RouteGeneration) > 0)
                {
                    if (context.RealtimeCache?.Find(changed.Position.Id.Trade) is { } closing && closing.Apply(changed.Position))
                        await context.SendAsync<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent, TradeEntityId>(
                            TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent.From(closing.ReadForPublication())).ConfigureAwait(false);
                    context.RealtimeCache?.Remove(changed.Position.Id.Trade);
                }
                return;
            }

            foreach (var leg in changed.Position.Legs)
            {
                var route = new PortfolioFundTradeLeg(changed.Position.Id.Trade.PortfolioId, changed.Position.Id.Trade.FundId, changed.Position.Id.Trade.OrderId, changed.Position.Id.Trade.TradeId, changed.Position.Id.PositionId, leg.TradeLegId, changed.Position.StrategyKind, changed.Position.RouteGeneration);
                if (FuturesOptionRoutePolicy.IsSupported(route))
                    context.RouteIndex.Add(route, leg.ContractId);
            }

            if (context.RealtimeCache is { } registry && changed.Position.StrategyKind == TradeStrategyKind.IronCondor)
            {
                var cache = registry.Find(changed.Position.Id.Trade);
                if (cache is null || cache.Read().ValueDate != changed.Position.ValueDate
                    || !cache.Read().Legs.Select(leg => (leg.Definition.TradeLegId, leg.Definition.ContractId))
                        .OrderBy(leg => leg.TradeLegId).SequenceEqual(changed.Position.Legs.Select(leg => (leg.TradeLegId, leg.ContractId)).OrderBy(leg => leg.TradeLegId)))
                {
                    var trade = await context.DbFactory.TradeDb.GetEstablishedTradeAsync(changed.Position.Id.Trade).ConfigureAwait(false);
                    if (trade is not null) cache = registry.Register(trade, changed.Position.ValueDate);
                }
                if (cache?.Apply(changed.Position) == true)
                    await context.SendAsync<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent, TradeEntityId>(
                        TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent.From(cache.ReadForPublication())).ConfigureAwait(false);
            }
        }
    }
    static async ValueTask ExecuteDurableAsync(OpenPositionRoutesChangedEvent changed,IFuturesOptionRealtimeContext context,PositionRealtimeIntents store)
    {
        var position=changed.Position;
        if(store.IsRetired(position))return;
        var key=TradePlanContractIdentity.DeterministicId($"option-position-lifecycle/v1|{position.Id.Format()}|{position.RouteGeneration}|{position.PositionSequence}");
        var hash=PositionRealtimeIntents.Hash(position);
        var intent=await store.FindAsync(key,hash).ConfigureAwait(false);
        if(intent is null)
        {
            byte[] output=[];string? outputOrigin=null;
            if(context.RealtimeCache is {} registry && position.StrategyKind==TradeStrategyKind.IronCondor)
            {
                var cache=registry.Find(position.Id.Trade);
                if(cache is null || !Compatible(cache,position))
                {
                    var trade=await context.DbFactory.TradeDb.GetEstablishedTradeAsync(position.Id.Trade).ConfigureAwait(false);
                    if(trade is not null)cache=new TradeRealtimeHotCache(trade,position.ValueDate);
                }
                if(cache is not null)
                {
                    var notification=MessageOrigins.Copy(changed,TradeRealtimeSnapshotChangedEvent.From(cache.PreparePositionPublication(position)));
                    output=MessagePackSerializer.Serialize(notification);
                    if(MessageOrigins.Get(notification) is {} capturedOrigin)outputOrigin=MessageOriginBinary.Serialize(capturedOrigin);
                }
            }
            intent=await store.CaptureAsync(key,hash,()=>new(){Kind=PositionIntentKind.Lifecycle,PositionId=position.Id,
                RouteGeneration=position.RouteGeneration,PositionSequence=position.PositionSequence,Request=MessagePackSerializer.Serialize(changed),
                Output=output,OutputOrigin=outputOrigin,Origin=MessageOrigins.Get(changed) is {} origin ? MessageOriginBinary.Serialize(origin):null,Flags=PositionIntentFlags.OutputCaptured}).ConfigureAwait(false);
        }
        if(intent.IsCompleted)return;
        // Durable control evidence owns the transition before routes or cache are changed.
        if(position.IsOpen)
        {
            foreach(var leg in position.Legs)
            {
                var route=new PortfolioFundTradeLeg(position.Id.Trade.PortfolioId,position.Id.Trade.FundId,position.Id.Trade.OrderId,position.Id.Trade.TradeId,position.Id.PositionId,leg.TradeLegId,position.StrategyKind,position.RouteGeneration);
                if(FuturesOptionRoutePolicy.IsSupported(route))context.RouteIndex.Add(route,leg.ContractId);
            }
            if(context.RealtimeCache is {} registry && position.StrategyKind==TradeStrategyKind.IronCondor)
            {
                var cache=registry.Find(position.Id.Trade);
                if(cache is null || !Compatible(cache,position))
                {
                    var trade=await context.DbFactory.TradeDb.GetEstablishedTradeAsync(position.Id.Trade).ConfigureAwait(false);
                    if(trade is not null)cache=registry.Register(trade,position.ValueDate);
                }
                cache?.Apply(position);
            }
        }
        else
        {
            context.RouteIndex.RemovePosition(position.Id.PositionId,position.RouteGeneration);
            context.RealtimeCache?.Find(position.Id.Trade)?.Apply(position);
        }
        if((intent.Flags&PositionIntentFlags.Published)==0)
        {
            // Notify is latest-state presentation, not a financial acknowledgement.
            if(intent.Output.Length>0)
            {
                var notification=MessagePackSerializer.Deserialize<TradeRealtimeSnapshotChangedEvent>(intent.Output);
                MessageOrigins.Restore(notification,intent.OutputOrigin is {} origin ? MessageOriginBinary.Deserialize(origin):null);
                await context.SendAsync<TradeRealtimeSnapshotChangedEvent,TradeEntityId>(notification).ConfigureAwait(false);
            }
            await store.AdvanceAsync(key,PositionIntentFlags.Published).ConfigureAwait(false);
        }
        if(!position.IsOpen && context.RealtimeCache?.Find(position.Id.Trade) is {} closing)
        {
            var current=closing.Read().Position;
            if(current is null || current.RouteGeneration<position.RouteGeneration || current.RouteGeneration==position.RouteGeneration && current.PositionSequence<=position.PositionSequence)
                context.RealtimeCache.Remove(position.Id.Trade);
        }
        await store.AdvanceAsync(key,PositionIntentFlags.Cleaned|PositionIntentFlags.Completed).ConfigureAwait(false);
    }

    static bool Compatible(TradeRealtimeHotCache cache,StrategyPositionSnapshot position)
    {
        var snapshot=cache.Read();
        return snapshot.ValueDate==position.ValueDate && snapshot.Legs.Select(leg=>(leg.Definition.TradeLegId,leg.Definition.ContractId)).OrderBy(leg=>leg.TradeLegId)
            .SequenceEqual(position.Legs.Select(leg=>(leg.TradeLegId,leg.ContractId)).OrderBy(leg=>leg.TradeLegId));
    }

    public static async ValueTask RecoverAsync(IFuturesOptionRealtimeContext context)
    {
        if(context.PositionIntents is not {} store)return;
        foreach(var pending in store.Pending(PositionIntentKind.Lifecycle))
        {
            var changed=MessagePackSerializer.Deserialize<OpenPositionRoutesChangedEvent>(pending.Request);
            MessageOrigins.Restore(changed,pending.Origin is {} origin ? MessageOriginBinary.Deserialize(origin):null);
            // Missing routes are not proof of closure. Reconcile only actual authoritative position state.
            var actual=await context.DbFactory.TradeDb.GetStrategyPositionAsync(changed.Position.Id).ConfigureAwait(false);
            if(actual is {IsOpen:false} && (actual.RouteGeneration>changed.Position.RouteGeneration || actual.RouteGeneration==changed.Position.RouteGeneration && actual.PositionSequence>=changed.Position.PositionSequence))
            {
                if(changed.Position.IsOpen)
                {
                    context.RouteIndex.RemovePosition(actual.Id.PositionId,actual.RouteGeneration);
                    if(context.RealtimeCache?.Find(actual.Id.Trade) is {} cache && (cache.Read().Position is not {} current || current.RouteGeneration<=actual.RouteGeneration))
                        context.RealtimeCache.Remove(actual.Id.Trade);
                    await store.RetireClosedAsync(actual,changed.Id).ConfigureAwait(false);
                    continue;
                }
            }
            await ExecuteDurableAsync(changed,context,store).ConfigureAwait(false);
        }
    }

}
