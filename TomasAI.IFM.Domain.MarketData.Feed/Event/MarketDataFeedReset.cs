using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedReset
{
    static MarketDataFeedReset()
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
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedResetEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedResetEvent for EntityId: {e.EntityId}";
        try
        {
            p.MarketDataApi.Stop();
            await Task.Delay(TimeSpan.FromSeconds(2));
            var started = p.MarketDataApi.Start(async (errorCode, errorMsg) => await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, errorCode, errorMsg));
            if (started)
            {
                await context.MarketDataFeedResetCompleteAsync(e);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed reset");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: market data feed reset", source);
                return true;
            }
            else
            {
                throw new InvalidOperationException("Market data API failed to start for unknown reasons.");
            }
        }
        catch (Exception ex)
        {
            await context.MarketDataFeedResetFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedResetEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed reset failed", source);
        }
        return false;
    }
   
}
