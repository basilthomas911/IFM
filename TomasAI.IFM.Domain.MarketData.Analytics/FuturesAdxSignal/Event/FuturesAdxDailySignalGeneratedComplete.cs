using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event;

/// <summary>Handles the FuturesAdxDailySignalGeneratedCompleteEvent message in the FuturesAdxSignalEventActor lifecycle.</summary>
public static class FuturesAdxDailySignalGeneratedComplete
{
    static FuturesAdxDailySignalGeneratedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesAdxSignalEvent}";
    }

    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// Handles the completion of an ADX daily signal generation event.
    /// </summary>
    /// <param name="e">The completed event containing the generated ADX signal.</param>
    /// <param name="context">The typed ADX event context that exposes handler dependencies.</param>
    /// <param name="logger">The logger used to log messages to the application logs.</param>
    /// <returns><see langword="true"/> if the handler completed successfully; otherwise <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesAdxDailySignalGeneratedCompleteEvent e,
        IFuturesAdxSignalEventContext context,
        ILogger<FuturesAdxSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(e);
        var source = $"FuturesAdxDailySignalGeneratedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write3(logger, ex, source);
            await (context.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesAdxSignalEvent, FuturesAdxDailySignalGeneratedCompleteEvent.ErrorCode, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
