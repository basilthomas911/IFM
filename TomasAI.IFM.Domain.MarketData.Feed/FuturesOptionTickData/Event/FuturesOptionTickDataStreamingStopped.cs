using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickDataStreamingStopped
{
    static FuturesOptionTickDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStoppedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedEventApi eventApi,
    FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            _ = await p.Readers.ReleaseAsync(
                e.ContractId,
                FuturesOptionTickDataStreamingStarted.CreateOwner(
                    e.EntityId,
                    e.ContractId));
            await eventApi.SendFuturesOptionTickDataStreamingStoppedCompleteAsync(e);

            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"{e.ContractId} Streaming Stopped");
            p.Logger.LogInformationEvent("{Source}: futures option {ContractId} streaming stopped", source, e.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            await eventApi.SendFuturesOptionTickDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, 6008, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming stop failed", source, e.ContractId);
        }
        return false;
    }
}
