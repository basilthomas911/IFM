using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedStarted
{
    static MarketDataFeedStarted()
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
        this MarketDataFeedStartedEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStartedEvent for EntityId: {e.EntityId}";
        try
        {
            var started = p.MarketDataApi.Start(async (errorCode, errorMsg) => await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, errorCode, errorMsg));
            if (started)
            {
                await context.SendMarketDataFeedStartedCompleteAsync(e);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed started");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: market data feed started", source);
            }
            else
                throw new InvalidOperationException("Market data API failed to start for unknown reasons.");
            return true;
        }
        catch (Exception ex)
        {
            await context.SendMarketDataFeedStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStartedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed start failed", source);
        }
        return false;
    }
    
}
