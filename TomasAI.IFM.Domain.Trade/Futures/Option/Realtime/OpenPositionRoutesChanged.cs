using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
public static class OpenPositionRoutesChanged
{
    public static async ValueTask ExecuteAsync(this OpenPositionRoutesChangedEvent changed, IFuturesOptionRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(context);
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
