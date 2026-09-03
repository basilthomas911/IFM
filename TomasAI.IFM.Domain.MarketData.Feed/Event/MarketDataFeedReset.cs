using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

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
        this MarketDataFeedResetEvent e,
        IEventActorContext context,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedResetEvent for EntityId: {e.EntityId}";
        try
        {
            await p.MarketDataLifecycle.ResetAsync(e.ValueDate, e.CommandId);
            await eventApi.MarketDataFeedResetCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, "Market data feed reset");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: market data feed reset", source);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.MarketDataFeedResetFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, MarketDataFeedResetEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed reset failed", source);
        }
        return false;
    }
   
}
