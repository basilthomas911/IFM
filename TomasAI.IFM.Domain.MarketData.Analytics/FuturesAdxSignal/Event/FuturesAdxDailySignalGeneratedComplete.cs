using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

public static class FuturesAdxDailySignalGeneratedComplete
{
    static FuturesAdxDailySignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesAdxSignalEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of an ADX daily signal generation event.
    /// </summary>
    /// <param name="e">The completed event containing the generated ADX signal.</param>
    /// <param name="context">The typed ADX event context that exposes handler dependencies.</param>
    /// <param name="logger">The logger used to log messages to the application logs.</param>
    /// <returns><see langword="true"/> if the handler completed successfully; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAdxDailySignalGeneratedCompleteEvent e, 
        IFuturesAdxSignalEventContext context,
        ILogger logger)
    {
        var source = $"FuturesAdxDailySignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            await context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAdxSignalEvent, FuturesAdxDailySignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogError(ex, "{Source}:  event handler failed", source);
        }
        return false;
    }
}
