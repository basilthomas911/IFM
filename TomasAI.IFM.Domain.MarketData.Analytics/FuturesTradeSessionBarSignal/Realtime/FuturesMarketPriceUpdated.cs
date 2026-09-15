using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles normalized futures trades routed to the trade-session bar signal.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Accumulates one trade and forwards every newly completed bar to the Command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent @event,
        IFuturesTradeSessionBarSignalRealtimeContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        var accumulatorId = new FuturesTradeSessionBarAccumulatorEntityId(@event.EntityId.ValueDate);
        foreach (var bar in context.Accumulators.Get(accumulatorId).Accept(@event))
        {
            var result = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
            if (!result.Success && result.ErrorMessage?.StartsWith("BAR.EXCEPTION;", StringComparison.Ordinal) != true)
                logger.LogError(
                    "Trade-session bar publication failed. ContractId={ContractId} TimeFrame={TimeFrame} ObservationId={ObservationId} IntervalEndUtc={IntervalEndUtc} FirstSourceSequence={FirstSourceSequence} LastSourceSequence={LastSourceSequence} LastMarketEventUtc={LastMarketEventUtc} CalculatedAtUtc={CalculatedAtUtc} StreamEpochId={StreamEpochId} ErrorCode={ErrorCode} Error={Error}",
                    bar.ContractId, bar.TimeFrame, bar.ObservationId.Value, bar.IntervalEndUtc,
                    bar.FirstSourceSequence, bar.LastSourceSequence, bar.LastMarketEventUtc,
                    bar.CalculatedAtUtc, bar.StreamEpochId, result.ErrorCode, result.ErrorMessage);
        }
        return true;
    }
}
