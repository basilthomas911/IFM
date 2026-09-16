using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Event;

/// <summary>Handles the successful terminal event for YieldCurveRatesImported.</summary>
public static class YieldCurveRatesImportedComplete
{
    /// <summary>Forwards the terminal download outcome without starting another import.</summary>
    public static ValueTask<bool> ExecuteAsync(this YieldCurveRatesImportedCompleteEvent eventValue, IEventActorContext context, ILogger<YieldCurveRateEventActor> logger)
        => DownloadLogDelivery.ForwardAsync(eventValue.DownloadOutcome, eventValue, MarketDataDownloadDataset.TreasuryCurve, MarketDataDownloadStatus.Completed, context, logger);
}
