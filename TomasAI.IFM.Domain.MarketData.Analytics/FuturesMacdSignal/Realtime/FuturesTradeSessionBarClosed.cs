using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime;

/// <summary>Handles closed observations routed to the stateless MACD realtime actor.</summary>
public static class FuturesTradeSessionBarClosed
{
    /// <summary>Forwards one MACD command for every matching attached identity.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesMacdSignalRealtimeContext context, ILogger logger)
    {
        var observation = IsArgumentNull.Set(@event.Observation);
        if (!observation.IsValid) return true;
        try
        {
            foreach (var entityId in FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>
                         .Snapshot().Where(entityId => Matches(entityId, observation)))
            {
                var signalId = new FuturesMacdSignalId(entityId.ContractId, entityId.ValueDate,
                    entityId.TimePeriod, entityId.SignalEmaPeriod, entityId.FastEmaPeriod,
                    entityId.SlowEmaPeriod, TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
                _ = await context.GenerateFuturesMacdSignalAsync(signalId, observation.Close, observation)
                    .ConfigureAwait(false);
            }
            return true;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(nameof(FuturesTradeSessionBarClosedRealtimeEvent), exception,
                "Unable to forward observation {ObservationId} to the MACD command actor", observation.ObservationId);
            return false;
        }
    }

    static bool Matches(FuturesMacdSignalEntityId entityId, FuturesTradeSessionBarReadModel observation) =>
        StringComparer.Ordinal.Equals(entityId.ContractId, observation.ContractId)
        && entityId.ValueDate == observation.ValueDate
        && entityId.TimePeriod == observation.TimeFrame;
}
