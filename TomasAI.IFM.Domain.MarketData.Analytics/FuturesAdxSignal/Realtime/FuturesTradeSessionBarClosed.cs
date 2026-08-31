using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime;

/// <summary>Handles shared closed observations routed to the stateless ADX realtime actor.</summary>
public static class FuturesTradeSessionBarClosed
{
    /// <summary>Sends one event-sourced ADX Generate command for every matching active ADX identity.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesAdxSignalRealtimeContext context,
        ILogger logger)
    {
        IsArgumentNull.Check(@event);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(logger);
        var observation = IsArgumentNull.Set(@event.Observation);
        if (!observation.IsValid)
            return true;

        try
        {
            var succeeded = true;
            foreach (var entityId in FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>
                         .Snapshot()
                         .Where(entityId => Matches(entityId, observation)))
            {
                var signalId = new FuturesAdxSignalId(
                    entityId.ContractId,
                    entityId.ValueDate,
                    entityId.TimePeriod,
                    entityId.PeriodLength,
                    TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
                var result = await context.GenerateFuturesAdxSignalAsync(
                        signalId,
                        observation.Close,
                        observation)
                    .ConfigureAwait(false);
                if (result is ServiceFailed<GuidResult> failed)
                {
                    succeeded = false;
                    logger.LogError(
                        "ADX command rejected observation {ObservationId} for {EntityId}: {ErrorMessage}",
                        observation.ObservationId, entityId, failed.ErrorMessage);
                }
                else
                    logger.LogDebug(
                        "Forwarded observation {ObservationId} to the ADX command actor for {EntityId}",
                        observation.ObservationId,
                        entityId);
            }
            return succeeded;
        }
        catch (Exception exception)
        {
            logger.LogErrorEvent(
                nameof(FuturesTradeSessionBarClosedRealtimeEvent),
                exception,
                "Unable to forward observation {ObservationId} to the ADX command actor",
                observation.ObservationId);
            return false;
        }
    }

    static bool Matches(
        FuturesAdxSignalEntityId entityId,
        FuturesTradeSessionBarReadModel observation) =>
        StringComparer.Ordinal.Equals(entityId.ContractId, observation.ContractId)
        && entityId.ValueDate == observation.ValueDate
        && entityId.TimePeriod == observation.TimeFrame;
}
