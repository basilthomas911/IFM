using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

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
        this MarketDataFeedStartedEvent e,
        IEventActorContext context,
        IActorMarketDataFeedEventApi eventApi,
        MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStartedEvent for EntityId: {e.EntityId}";
        try
        {
            await p.MarketDataApi.StartAsync(
                e.ValueDate,
                (_, errorCode, errorMsg) => p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent, errorCode, errorMsg));
            await eventApi.SendMarketDataFeedStartedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed started");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: market data feed started", source);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.SendMarketDataFeedStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedStartedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed start failed", source);
        }
        return false;
    }
    
}
