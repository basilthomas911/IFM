using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

public static class OpenPositionRoutesChanged
{
    public static ValueTask ExecuteAsync(
        this OpenPositionRoutesChangedEvent changed,
        IFuturesOptionRealtimeContext context)
    {
        if (!changed.Position.IsOpen)
        {
            context.RouteIndex.RemovePosition(
                changed.Position.Id.PositionId,
                changed.Position.RouteGeneration);
            return ValueTask.CompletedTask;
        }

        foreach (var leg in changed.Position.Legs)
        {
            var route = new PortfolioFundTradeLeg(
                changed.Position.Id.Trade.PortfolioId,
                changed.Position.Id.Trade.FundId,
                changed.Position.Id.Trade.OrderId,
                changed.Position.Id.Trade.TradeId,
                changed.Position.Id.PositionId,
                leg.TradeLegId,
                changed.Position.StrategyKind,
                changed.Position.RouteGeneration);
            if (FuturesOptionRoutePolicy.IsSupported(route))
                context.RouteIndex.Add(route, leg.ContractId);
        }

        return ValueTask.CompletedTask;
    }
}
