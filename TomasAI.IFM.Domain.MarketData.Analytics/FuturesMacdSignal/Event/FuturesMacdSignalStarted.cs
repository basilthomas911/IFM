using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

/// <summary>Handles MACD lifecycle start events.</summary>
public static class FuturesMacdSignalStarted
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Attaches the MACD identity to shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStartedEvent @event,
        IFuturesMacdSignalEventContext context, ILogger<FuturesMacdSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Attach(@event.EntityId);
            if (@event.RestoredSignal is { IsWarm: true, Metadata.IsValid: true })
                await ((TomasAI.IFM.Shared.EventModelActor.Contracts.IEventActorContext<FuturesMacdSignalEventActor>)context)
                    .PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write39(logger, exception, @event.EntityId);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent,
                FuturesMacdSignalStartedEvent.ErrorCode, exception.GetErrorMessage())).ConfigureAwait(false);
            return false;
        }
    }
}
