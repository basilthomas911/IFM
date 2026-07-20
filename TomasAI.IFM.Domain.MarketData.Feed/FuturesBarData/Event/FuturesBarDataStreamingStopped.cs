using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

public static class FuturesBarDataStreamingStopped
{
    static FuturesBarDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesBarDataEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="context"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(this FuturesBarDataStreamingStoppedEvent e, IEventActorContext context, FuturesBarDataEventParameters p)
    {
        var source = $"FuturesBarDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        var stopped = false;
        try
        {
            p.FuturesBarDataTimer.Stop();
            await context.FuturesBarDataStreamingStoppedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, source);
            p.Logger.LogInformationEvent(ServiceId, "{Source}", source);
            stopped = true;
        }
        catch (Exception ex)
        {
            await context.FuturesBarDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStoppedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data streaming stop failed", source);
        }
        return stopped;
    }

}
