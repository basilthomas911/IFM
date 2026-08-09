using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public static class FuturesTickDataStreamingStopped
{
    static FuturesTickDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }

public static async ValueTask<bool> ExecuteAsync(
    this FuturesTickDataStreamingStoppedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedEventApi eventApi,
    FuturesTickDataEventParameters p)
    {
        var source = $"FuturesTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            var streamId = p.MarketDataApi.StreamIds[e.ContractId];
            if (streamId == -1)
                throw new InvalidOperationException($"Stream ID not found for futures contract {e.ContractId}.");
            if (!p.MarketDataApi.StopStreamingFuturesTickData(streamId))
                throw new InvalidOperationException($"Market data API failed to stop streaming futures contract {e.ContractId}.");

            p.MarketDataApi.StreamIds.Remove(streamId);
            await eventApi.FuturesTickDataStreamingStoppedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, $"futures tick data {e.ContractId} streaming stopped");
            p.Logger.LogInformationEvent(ServiceId, "{Source}: futures tick data {e.ContractId} streaming stopped", source, e.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.FuturesTickDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, 6005, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures tick data {e.ContractId} streaming stop failed", source, e.ContractId);
        }
        return false;
    }

}
