using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedStoppedComplete
{
    static MarketDataFeedStoppedComplete()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="statusConsoleWriter"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStoppedCompleteEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStoppedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            //await state.StopFuturesBarDataStreamingAsync(context, e.ValueDate);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Futures bar data streaming stopped");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: futures bar data streaming stopped", source);
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStoppedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: stopping futures bar data feed failed", source);
        }
        return false;
    }
}
