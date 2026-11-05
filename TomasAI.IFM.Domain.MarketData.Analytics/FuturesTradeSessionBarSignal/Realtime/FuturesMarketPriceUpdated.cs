using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles normalized futures trades routed to the trade-session bar signal.</summary>
public static class FuturesMarketPriceUpdated
{

    extension(FuturesMarketPriceUpdatedRealtimeEvent @event)
    {
        /// <summary>Accumulates one trade and forwards every newly completed bar to the Command actor.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesTradeSessionBarSignalRealtimeContext context,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            var accumulatorId = context.Accumulators.Resolve(@event.EntityId.ContractId, @event.EntityId.ValueDate);
            var bars = context.Accumulators.Get(accumulatorId).Accept(@event);
            if (bars.Count == 0) return true;
            await Task.WhenAll(bars.GroupBy(bar => bar.TimeFrame).Select(async frame =>
            {
                foreach (var bar in frame)
                {
                    var result = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
                    if (!result.Success && result.ErrorMessage?.StartsWith("BAR.EXCEPTION;", StringComparison.Ordinal) != true)
                    {

                        var diagnosticLogger = logger;
                        if (diagnosticLogger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                        {
                            global::TomasAI.IFM.Domain.MarketData.Analytics.StorageMigrationDiagnosticLog.Write10(diagnosticLogger, null, bar.ContractId, bar.TimeFrame, bar.ObservationId.Value, bar.IntervalEndUtc, bar.FirstSourceSequence, bar.LastSourceSequence, bar.LastMarketEventUtc, bar.CalculatedAtUtc, bar.StreamEpochId, result.ErrorCode, result.ErrorMessage);
                        }

                    }
                }
            })).ConfigureAwait(false);
            return true;
        }
    }
}
