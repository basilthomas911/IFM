using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;

/// <summary>Handles the failed terminal event for EconomicCalendarsImported.</summary>
public static class EconomicCalendarsImportedFail
{

    extension(EconomicCalendarsImportedFailEvent eventValue)
    {
        /// <summary>Forwards the terminal download outcome without starting another import.</summary>
        public ValueTask<bool> ExecuteAsync(IEventActorContext context, ILogger<EconomicCalendarEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(eventValue);
            return DownloadLogDelivery.ForwardAsync(eventValue.DownloadOutcome, eventValue, MarketDataDownloadDataset.EconomicCalendar, MarketDataDownloadStatus.Failed, context, logger);
        }
    }
}
