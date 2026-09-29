using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

/// <summary>Handles ATR lifecycle start events.</summary>
public static class FuturesAtrSignalStarted
{
    /// <summary>Attaches the ATR identity to shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStartedEvent @event,
        IFuturesAtrSignalEventContext context, ILogger<FuturesAtrSignalEventActor> logger)
    {
        try
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Attach(@event.EntityId);
            if (@event.RestoredSignal is { IsWarm: true, AtrValue: > 0d, AtrRatio: not null, Metadata.IsValid: true })
                await ((TomasAI.IFM.Shared.EventModelActor.Contracts.IEventActorContext<FuturesAtrSignalEventActor>)context)
                    .PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to attach ATR observation identity {EntityId}", @event.EntityId);
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent,
                FuturesAtrSignalStartedEvent.ErrorCode, exception.GetErrorMessage());
            return false;
        }
    }
}
