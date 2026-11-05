using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;

/// <summary>Handles the failed terminal event for YieldCurveRatesImported.</summary>
public static class YieldCurveRatesImportedFail
{

    static string ServiceId => nameof(LogSourceType.YieldCurveRatesImported);

    extension(YieldCurveRatesImportedFailEvent eventValue)
    {
        /// <summary>Forwards the terminal download outcome without starting another import.</summary>
        public ValueTask<bool> ExecuteAsync(IEventActorContext context, ILogger<YieldCurveRateEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(eventValue);
            logger.LogErrorEvent(ServiceId, null,
                "Import failed. EventId={EventId} CommandId={CommandId} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
                eventValue.Id, eventValue.CommandId, eventValue.ErrorCode, eventValue.ErrorMessage);
            return DownloadLogDelivery.ForwardAsync(eventValue.DownloadOutcome, eventValue, MarketDataDownloadDataset.TreasuryCurve, MarketDataDownloadStatus.Failed, context, logger);
        }
    }
}
