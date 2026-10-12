using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime;

/// <summary>Handles closed observations routed to the stateless MACD realtime actor.</summary>
public static class FuturesTradeSessionBarClosed
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Forwards one MACD command for every matching attached identity.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesMacdSignalRealtimeContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        var observation = IsArgumentNull.Set(@event.Observation);
        if (!observation.IsValid) return true;
        try
        {
            var succeeded = true;
            foreach (var entityId in FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>
                         .Snapshot().Where(entityId => Matches(entityId, observation)))
            {
                var signalId = new FuturesMacdSignalId(entityId.ContractId, entityId.ValueDate,
                    entityId.TimePeriod, entityId.SignalEmaPeriod, entityId.FastEmaPeriod,
                    entityId.SlowEmaPeriod, TimeOnly.FromDateTime(observation.LastMarketEventUtc.UtcDateTime));
                var result = await context.GenerateFuturesMacdSignalAsync(signalId, observation.Close, observation)
                    .ConfigureAwait(false);
                FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Observe(entityId, observation.LastMarketEventUtc, result is not ServiceFailed<GuidResult>);
                if (result is ServiceFailed<GuidResult> failed)
                {
                    succeeded = false;
                    global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write36(logger, null, observation.ObservationId, entityId, failed.ErrorMessage);
                }
            }
            return succeeded;
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
