using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

/// <summary>Handles MACD lifecycle start events.</summary>
public static class FuturesMacdSignalStarted
{
    /// <summary>Attaches the MACD identity to shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStartedEvent @event,
        IFuturesMacdSignalEventContext context, ILogger logger)
    {
        try { FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Attach(@event.EntityId); return true; }
        catch (Exception exception)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent,
                FuturesMacdSignalStartedEvent.ErrorCode, exception.GetErrorMessage());
            logger.LogError(exception, "Unable to attach MACD observation identity {EntityId}", @event.EntityId);
            return false;
        }
    }
}
