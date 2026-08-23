using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public static class MarketDataFeedResetComplete
{
    static MarketDataFeedResetComplete()
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
        this MarketDataFeedResetCompleteEvent e,
        IEventActorContext context,
        IEventActorContext commandApi,
        IEventActorContext eventApi,
        MarketDataFeedEventParameters p)
    {
        var source = $"MarketDataFeedResetCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            foreach (var futuresContract in e.FuturesContracts)
            {
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Reset streaming of Futures {futuresContract.ContractId}...");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: reset streaming of Futures {ContractId}...", source, futuresContract.ContractId);
                await Task.Delay(TimeSpan.FromSeconds(2));
                var entityId = new FuturesDataId(futuresContract.ContractId, e.ValueDate);
                await commandApi.StartFuturesTickDataStreamingAsync(e, futuresContract, entityId);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Reset streaming of Futures {futuresContract.ContractId} started");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: reset streaming of Futures {ContractId} started", source, futuresContract.ContractId);
            }
            var streamingEntityId = new FuturesBarDataStreamingId(e.ValueDate);
            await commandApi.StartFuturesBarDataStreamingAsync(e, streamingEntityId);
            await Task.Delay(TimeSpan.FromSeconds(1));
            await eventApi.SendResetStreamingEventAsync(e);
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: data feed reset complete failed");
        }
        return false;
    }

}
