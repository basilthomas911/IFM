using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

namespace TomasAI.IFM.Application.MarketData.Recovery;

/// <summary>Seals the prior position session before trade demand can start.</summary>
public sealed class TradeFeedEndOfDayCatchUp(
    Func<StrategyPositionId, CancellationToken, Task<StrategyPositionSnapshot?>> loadPosition,
    IStrategyPositionCommandApi positions)
{
    public async Task EnsureAsync(EstablishedTradeDefinition trade, DateOnly nextValueDate, CancellationToken token)
    {
        if (trade.Status != EstablishedTradeStatus.Open
            || trade.StrategyKind is not (TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread or TradeStrategyKind.FuturesOutright))
            return;

        var positionId = StrategyPositionId.Create(trade.Id, trade.StrategyKind);
        var snapshot = await loadPosition(positionId, token).ConfigureAwait(false);
        if (PositionEndOfDayCatchUp.RequiredValueDate(snapshot, nextValueDate) is not { } previousDate)
            return;

        var boundary = FuturesTradingValueDate.GetSessionEndUtc(previousDate).UtcDateTime;
        var result = await positions.EndOfDayAsync(positionId, trade.StrategyKind, previousDate, boundary, token).ConfigureAwait(false);
        // The first routed quote or the scheduler can commit the same seal concurrently.
        var persisted = await loadPosition(positionId, token).ConfigureAwait(false);
        if (persisted?.LatestSealedValueDate >= previousDate)
            return;

        throw new InvalidOperationException($"Prior-session EOD was not committed for {positionId.Format()} on {previousDate:yyyy-MM-dd}: {result.ErrorMessage}");
    }
}
