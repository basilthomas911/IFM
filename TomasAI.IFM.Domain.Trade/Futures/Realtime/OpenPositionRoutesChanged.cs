using TomasAI.IFM.Domain.Trade.Futures.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime;

/// <summary>Updates Futures outright routes from one open-position route change.</summary>
public static class OpenPositionRoutesChanged
{
    /// <summary>Adds the current position routes or removes routes for a closed or unsupported position.</summary>
    public static ValueTask ExecuteAsync(
        this OpenPositionRoutesChangedEvent changed,
        IFuturesRealtimeContext context)
    {
        if (!changed.Position.IsOpen || changed.Position.StrategyKind != TradeStrategyKind.FuturesOutright)
        {
            context.RouteIndex.RemovePosition(changed.Position.Id.PositionId, changed.Position.RouteGeneration);
            return ValueTask.CompletedTask;
        }

        foreach (var leg in changed.Position.Legs)
        {
            context.RouteIndex.Add(new PortfolioFundTradeLeg(
                changed.Position.Id.Trade.PortfolioId,
                changed.Position.Id.Trade.FundId,
                changed.Position.Id.Trade.OrderId,
                changed.Position.Id.Trade.TradeId,
                changed.Position.Id.PositionId,
                leg.TradeLegId,
                TradeStrategyKind.FuturesOutright,
                changed.Position.RouteGeneration), leg.ContractId);
        }

        return ValueTask.CompletedTask;
    }
}
