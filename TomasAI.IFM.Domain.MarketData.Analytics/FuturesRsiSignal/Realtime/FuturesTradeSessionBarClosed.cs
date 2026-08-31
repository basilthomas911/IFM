using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime;

/// <summary>Handles closed observations routed to the stateless RSI realtime actor.</summary>
public static class FuturesTradeSessionBarClosed
{
    /// <summary>Forwards one RSI command for every matching attached identity.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesRsiSignalRealtimeContext context, ILogger logger)
    {
        var observation = IsArgumentNull.Set(@event.Observation);
        if (!observation.IsValid) return true;
        try
        {
            var succeeded = true;
            foreach (var entityId in FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>
                         .Snapshot().Where(entityId => Matches(entityId, observation)))
            {
                var signalId = new FuturesRsiSignalId(entityId.ContractId, entityId.ValueDate,
                    entityId.TimePeriod, entityId.PeriodLength,
                    TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
                var result = await context.GenerateFuturesRsiSignalAsync(signalId, observation.Close,
                    observation.LastSourceSequence, observation.LastMarketEventUtc.UtcDateTime, observation)
                    .ConfigureAwait(false);
                if (result is ServiceFailed<GuidResult> failed)
                {
                    succeeded = false;
                    logger.LogError(
                        "RSI command rejected observation {ObservationId} for {EntityId}: {ErrorMessage}",
                        observation.ObservationId, entityId, failed.ErrorMessage);
                }
            }
            return succeeded;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(nameof(FuturesTradeSessionBarClosedRealtimeEvent), exception,
                "Unable to forward observation {ObservationId} to the RSI command actor", observation.ObservationId);
            return false;
        }
    }

    static bool Matches(FuturesRsiSignalEntityId entityId, FuturesTradeSessionBarReadModel observation) =>
        StringComparer.Ordinal.Equals(entityId.ContractId, observation.ContractId)
        && entityId.ValueDate == observation.ValueDate
        && entityId.TimePeriod == observation.TimeFrame;
}
