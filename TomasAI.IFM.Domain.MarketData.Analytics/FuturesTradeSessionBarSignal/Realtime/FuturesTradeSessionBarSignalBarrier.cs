using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles the private server-clock barrier used to close elapsed trade-session bars.</summary>
public static class FuturesTradeSessionBarSignalBarrier
{
    /// <summary>Forwards every bar completed by the UTC barrier to the Command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarSignalBarrierRealtimeEvent @event,
        IFuturesTradeSessionBarSignalRealtimeContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        foreach (var bar in context.Accumulators.Get(@event.EntityId).CloseThrough(@event.BarrierUtc))
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
