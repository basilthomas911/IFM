using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event;

/// <summary>Handles MACD lifecycle stop events.</summary>
public static class FuturesMacdSignalStopped
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Detaches the MACD identity from shared closed observations.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesMacdSignalStoppedEvent @event,
        IFuturesMacdSignalEventContext context, ILogger<FuturesMacdSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        try { FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Detach(@event.EntityId); return true; }
        catch (Exception exception)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write37(logger, exception, @event.EntityId);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesMacdSignalEvent,
                FuturesMacdSignalStoppedEvent.ErrorCode, exception.GetErrorMessage())).ConfigureAwait(false);
            return false;
        }
    }
}
