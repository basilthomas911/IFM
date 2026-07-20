using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

public static class FuturesAtrSignalGeneratedComplete
{
    static FuturesAtrSignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesAtrSignalEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of the Futures ATR signal generation process. 
    /// This method is invoked when a FuturesAtrSignalGeneratedCompleteEvent is received, 
    /// indicating that the ATR signal has been successfully generated for a specific entity. 
    /// The handler can perform any necessary post-processing, such as updating the status console or logging the completion of the signal generation.
    /// </summary>
    /// <param name="e">The FuturesAtrSignalGeneratedCompleteEvent to handle.</param>
    /// <param name="context">The event actor context.</param>
    /// <param name="statusConsoleWriter">The status console writer.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAtrSignalGeneratedCompleteEvent e, 
        IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesAtrSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAtrSignalEvent, FuturesAtrSignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogError(ex, "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }
}
