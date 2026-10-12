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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Attaches the ATR identity to shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalStartedEvent @event,
        IFuturesAtrSignalEventContext context, ILogger<FuturesAtrSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
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
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write9(logger, exception, @event.EntityId);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent,
                FuturesAtrSignalStartedEvent.ErrorCode, exception.GetErrorMessage())).ConfigureAwait(false);
            return false;
        }
    }
}
