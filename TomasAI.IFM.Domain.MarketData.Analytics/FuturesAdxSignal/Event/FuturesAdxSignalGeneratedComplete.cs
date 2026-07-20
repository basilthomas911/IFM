using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

public static class FuturesAdxSignalGeneratedComplete
{
    static FuturesAdxSignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesAdxSignalEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of an ADX signal generation event.
    /// </summary>
    /// <param name="e">The completed event containing the generated ADX signal.</param>
    /// <param name="context">The event actor context used to dispatch downstream work.</param>
    /// <param name="statusConsoleWriter">The status console writer used to log messages to the status console.</param>
    /// <param name="logger">The logger used to log messages to the application logs.</param>
    /// <returns><see langword="true"/> if the handler completed successfully; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAdxSignalGeneratedCompleteEvent e, 
        IEventActorContext context, 
        IStatusConsoleWriter statusConsoleWriter, 
        ILogger logger)
    {
        var source = $"FuturesAdxSignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAdxSignalEvent, FuturesAdxSignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogError(ex, "{Source}:  {ContractId} complete handler failed", source, e.EntityId.ContractId);
        }
        return false;
    }


}
