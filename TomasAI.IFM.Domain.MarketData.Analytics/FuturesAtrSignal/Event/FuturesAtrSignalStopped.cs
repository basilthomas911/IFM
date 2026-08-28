using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

/// <summary>Handles ATR lifecycle stop events.</summary>
public static class FuturesAtrSignalStopped
{
    /// <summary>Detaches the ATR identity from shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStoppedEvent @event,
        IFuturesAtrSignalEventContext context, ILogger logger)
    {
        try { FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Detach(@event.EntityId); return true; }
        catch (Exception exception)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent,
                FuturesAtrSignalStoppedEvent.ErrorCode, exception.GetErrorMessage());
            logger.LogError(exception, "Unable to detach ATR observation identity {EntityId}", @event.EntityId);
            return false;
        }
    }
}
