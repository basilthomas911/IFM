using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

/// <summary>Handles MACD lifecycle stop events.</summary>
public static class FuturesMacdSignalStopped
{
    /// <summary>Detaches the MACD identity from shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStoppedEvent @event,
        IFuturesMacdSignalEventContext context, ILogger logger)
    {
        try { FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Detach(@event.EntityId); return true; }
        catch (Exception exception)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent,
                FuturesMacdSignalStoppedEvent.ErrorCode, exception.GetErrorMessage());
            logger.LogError(exception, "Unable to detach MACD observation identity {EntityId}", @event.EntityId);
            return false;
        }
    }
}
