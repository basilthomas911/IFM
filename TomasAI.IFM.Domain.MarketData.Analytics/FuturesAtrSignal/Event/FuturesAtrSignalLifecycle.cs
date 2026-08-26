using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

/// <summary>Provides the FuturesAtrSignalLifecycle implementation.</summary>
public static class FuturesAtrSignalLifecycle
{
    /// <summary>Executes the ExecuteAsync operation.</summary>
    public static ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStartedEvent e, IEventActorContext context,
        IEventActorContext commandApi, IMarketDataApi marketDataApi,
        IStatusConsoleWriter status, ILogger logger)
    {
        try
        {
            FuturesAnalyticsObservationAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(e.EntityId);
            return ValueTask.FromResult(true);
        }
        catch (Exception ex) { return HandleStartFailureAsync(ex); }

        async ValueTask LogAsync(Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent, FuturesAtrSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesAtrSignalEvent), ex.GetErrorMessage(),
                "ATR timer handler failed for {ContractId}", e.EntityId.ContractId);
        }
        async ValueTask<bool> HandleStartFailureAsync(Exception ex) { await LogAsync(ex); return false; }
    }

    /// <summary>Executes the ExecuteAsync operation.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStoppedEvent e, IEventActorContext context,
        IStatusConsoleWriter status, ILogger logger)
    {
        try
        {
            FuturesAnalyticsObservationAttachmentRegistry<FuturesAtrSignalEntityId>.Detach(e.EntityId);
            return true;
        }
        catch (Exception ex)
        {
            await status.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent, FuturesAtrSignalStoppedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(nameof(LogSourceType.FuturesAtrSignalEvent), ex.GetErrorMessage(),
                "ATR timer stop failed for {ContractId}", e.EntityId.ContractId);
            return false;
        }
    }
}
