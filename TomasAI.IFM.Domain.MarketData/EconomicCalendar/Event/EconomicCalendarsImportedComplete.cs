using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Event;

/// <summary>Handles the successful terminal event for EconomicCalendarsImported.</summary>
public static class EconomicCalendarsImportedComplete
{
    /// <summary>Forwards the terminal download outcome without starting another import.</summary>
    public static ValueTask<bool> ExecuteAsync(this EconomicCalendarsImportedCompleteEvent eventValue, IEventActorContext context, ILogger<EconomicCalendarEventActor> logger)
        => DownloadLogDelivery.ForwardAsync(eventValue.DownloadOutcome, eventValue, MarketDataDownloadDataset.EconomicCalendar, MarketDataDownloadStatus.Completed, context, logger);
}
