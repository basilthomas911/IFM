using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
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
        this MarketDataFeedStartedCompleteEvent e,
        IEventActorContext context,
        IActorMarketDataFeedCommandApi commandApi,
        MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedStartedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            foreach (var futuresContract in e.FuturesContracts!)
            {
                await p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent,
                    $"Starting to stream Futures {futuresContract.ContractId}...");
                p.Logger.LogInformationEvent(
                    ServiceId,
                    "{Source}: starting to stream Futures {ContractId}...",
                    source,
                    futuresContract.ContractId);
                var entityId = new FuturesDataId(futuresContract.ContractId, e.ValueDate);
                await commandApi.StartFuturesTickDataStreamingAsync(e, futuresContract, entityId);
                await p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent,
                    $"Streaming Futures {futuresContract.ContractId} started");
                p.Logger.LogInformationEvent(
                    ServiceId,
                    "{Source}: streaming Futures {ContractId} started",
                    source,
                    futuresContract.ContractId);
            }

            await commandApi.StartFuturesBarDataStreamingAsync(
                e,
                new FuturesBarDataStreamingId(e.ValueDate));
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
