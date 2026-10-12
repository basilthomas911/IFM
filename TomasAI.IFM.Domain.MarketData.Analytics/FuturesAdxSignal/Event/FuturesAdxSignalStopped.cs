using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

/// <summary>Handles <see cref="FuturesAdxSignalStoppedEvent"/> messages received by the ADX event actor.</summary>
public static class FuturesAdxSignalStopped
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>Detaches the ADX identity from the shared analytics observation stream.</summary>
    /// <param name="e">The ADX signal-stopped event.</param>
    /// <param name="context">The typed ADX event context that exposes handler dependencies.</param>
    /// <param name="logger">The logger used to record handler failures.</param>
    /// <returns><see langword="true"/> when the identity is detached; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesAdxSignalStoppedEvent e,
        IFuturesAdxSignalEventContext context,
        ILogger<FuturesAdxSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        try
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Detach(e.EntityId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(
                nameof(LogSourceType.FuturesAdxSignalEvent),
                ex,
                "ADX observation detachment failed for {ContractId}",
                e.EntityId.ContractId);
            await context.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.FuturesAdxSignalEvent,
                FuturesAdxSignalStoppedEvent.ErrorCode,
                ex.GetErrorMessage()).ConfigureAwait(false);
            return false;
        }
    }
}
