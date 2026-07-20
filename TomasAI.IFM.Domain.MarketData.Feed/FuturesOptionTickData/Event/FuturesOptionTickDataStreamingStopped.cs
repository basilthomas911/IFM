using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickDataStreamingStopped
{
    static FuturesOptionTickDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesOptionTickDataStreamingStoppedEvent e, IEventActorContext context, FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            var streamingRequestId = p.BlackboardService.StreamingRequestId.Get(e.ContractId);
            if (streamingRequestId.IsValid)
            {
                p.MarketDataApi.StopStreamingFuturesOptionTickData(streamingRequestId.RequestId);
                p.BlackboardService.StreamingRequestId.Remove(streamingRequestId);
                await context.SendFuturesOptionTickDataStreamingStoppedCompleteAsync(e);

                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"{e.ContractId} Streaming Stopped");
                p.Logger.LogInformationEvent("{Source}: futures option {ContractId} streaming stopped", source, e.ContractId);
            }
            else
                throw new InvalidOperationException($"Streaming request ID not found for contract {e.ContractId}.");
            return true;
        }
        catch (Exception ex)
        {
            await context.SendFuturesOptionTickDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, 6008, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming stop failed", source, e.ContractId);
        }
        return false;
    }
}
