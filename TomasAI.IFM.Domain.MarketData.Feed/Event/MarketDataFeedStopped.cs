using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedStopped
{
    static MarketDataFeedStopped()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } = default!;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStoppedEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            p.MarketDataApi.Stop();

            await context.SendMarketDataFeedStoppedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed stopped");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: market data feed stopped", source);
            return true;
        }
        catch (Exception ex)
        {
            await context.SendMarketDataFeedStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStoppedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed stop failed", source);
        }
        return false;
    }

 
}
