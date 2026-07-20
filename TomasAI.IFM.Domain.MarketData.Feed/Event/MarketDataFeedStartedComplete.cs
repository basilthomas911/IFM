using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedStartedComplete
{
    static MarketDataFeedStartedComplete()
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
        this MarketDataFeedStartedCompleteEvent e, IEventActorContext context, MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStartedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            foreach (var o in e.FuturesContracts!)
            {
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Starting to stream Futures {o.ContractId}...");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: starting to stream Futures {ContractId}...", source, o.ContractId);
                await Task.Delay(TimeSpan.FromSeconds(2));
                //await state.StartFuturesTickDataStreamingAsync(o, new FuturesDataId(o.ContractId, e.ValueDate));
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Streaming Futures {o.ContractId} started");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: streaming Futures {ContractId} started", source, o.ContractId);
            }
            //await state.StartFuturesBarDataStreamingAsync(new FuturesBarDataStreamingId(e.ValueDate));
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed start failed", source);
        }
        return false;
    }
}
