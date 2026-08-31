using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime;

/// <summary>Handles closed observations routed to the stateless ATR realtime actor.</summary>
public static class FuturesTradeSessionBarClosed
{
    /// <summary>Forwards one ATR command for every matching attached identity.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesAtrSignalRealtimeContext context,
        ILogger logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        var observation = IsArgumentNull.Set(@event.Observation);
        if (!observation.IsValid) return true;
        try
        {
            var succeeded = true;
            foreach (var entityId in FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>
                         .Snapshot().Where(entityId => Matches(entityId, observation)))
            {
                var signalId = new FuturesAtrSignalId(entityId.ContractId, entityId.ValueDate,
                    entityId.TimePeriod, entityId.PeriodLength,
                    TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
                var result = await context.GenerateFuturesAtrSignalAsync(signalId, observation.Close, observation)
                    .ConfigureAwait(false);
                if (result is ServiceFailed<GuidResult> failed)
                {
                    succeeded = false;
                    logger.LogError(
                        "ATR command rejected observation {ObservationId} for {EntityId}: {ErrorMessage}",
                        observation.ObservationId, entityId, failed.ErrorMessage);
                }
            }
            return succeeded;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(nameof(FuturesTradeSessionBarClosedRealtimeEvent), exception,
                "Unable to forward observation {ObservationId} to the ATR command actor", observation.ObservationId);
            return false;
        }
    }

    static bool Matches(FuturesAtrSignalEntityId entityId, FuturesTradeSessionBarReadModel observation) =>
        StringComparer.Ordinal.Equals(entityId.ContractId, observation.ContractId)
        && entityId.ValueDate == observation.ValueDate
        && entityId.TimePeriod == observation.TimeFrame;
}
