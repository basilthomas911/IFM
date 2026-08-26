using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

/// <summary>Handles <see cref="FuturesAdxSignalStartedEvent"/> messages received by the ADX event actor.</summary>
public static class FuturesAdxSignalStartedEventHandler
{
    /// <summary>Attaches the ADX identity to the shared analytics observation stream.</summary>
    /// <param name="e">The ADX signal-started event.</param>
    /// <param name="context">The typed ADX event context that exposes handler dependencies.</param>
    /// <param name="logger">The logger used to record handler failures.</param>
    /// <returns><see langword="true"/> when the identity is attached; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesAdxSignalStartedEvent e,
        IFuturesAdxSignalEventContext context,
        ILogger logger)
    {
        try
        {
            FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Attach(e.EntityId);
            return true;
        }
        catch (Exception ex)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(
                LogSourceType.FuturesAdxSignalEvent,
                FuturesAdxSignalStartedEvent.ErrorCode,
                ex.GetErrorMessage()).ConfigureAwait(false);
            logger.LogErrorEvent(
                nameof(LogSourceType.FuturesAdxSignalEvent),
                ex.GetErrorMessage(),
                "ADX observation attachment failed for {ContractId}",
                e.EntityId.ContractId);
            return false;
        }
    }
}
