using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

/// <summary>Handles ATR lifecycle start events.</summary>
public static class FuturesAtrSignalStarted
{
    /// <summary>Attaches the ATR identity to shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStartedEvent @event,
        IFuturesAtrSignalEventContext context, ILogger logger)
    {
        try { FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(@event.EntityId); return true; }
        catch (Exception exception)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent,
                FuturesAtrSignalStartedEvent.ErrorCode, exception.GetErrorMessage());
            logger.LogError(exception, "Unable to attach ATR observation identity {EntityId}", @event.EntityId);
            return false;
        }
    }
}
