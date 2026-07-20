using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;

public static class FuturesTradeSignalUpdatedComplete
{
    static FuturesTradeSignalUpdatedComplete()
    {
        ServiceId = $"{LogSourceType.FuturesTradeSignalEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// Handles the completion of a trade signal updated event.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSignalUpdatedCompleteEvent e, IEventActorContext context, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        var source = $"FuturesTradeSignalUpdatedCompleteEvent for CommandId: {e.CommandId}";
        try
        {
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTradeSignalEvent, 19001, ex.GetErrorMessage());
            logger.LogErrorEvent(ServiceId, ex.GetErrorMessage(), "{Source}: complete handler failed", source);
        }
        return false;
    }

}
