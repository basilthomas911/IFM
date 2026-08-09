using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public static class FuturesOptionTickDataStreamingStarted
{
    static FuturesOptionTickDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStartedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedEventApi eventApi,
    FuturesOptionTickDataEventParameters p)
    {
        var source = $"FuturesOptionTickDataStreamingStartedEvent for EntityId: {e.EntityId}";
        try
        {
            var started = await p.MarketDataApi.StartAsync(
                (errorCode, errorMsg) => p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, errorCode, errorMsg));
            if (started)
            {
                var streamingRequestId = p.BlackboardService.MarketDataFeed.StreamingRequestId.Get(e.Contract.ContractId);
                if (!streamingRequestId.IsValid)
                {
                    var requestId = await context.GetStreamingRequestIdQueryAsync(e.Contract.ContractId);
                    streamingRequestId = new(requestId, e.Contract, e.BaseContract, e.ValueDate, e.MaturityDate, e.RiskFreeRate);
                    p.BlackboardService.MarketDataFeed.StreamingRequestId.Set(streamingRequestId);
                }
                p.MarketDataApi.StartStreamingFuturesOptionTickData(streamingRequestId.RequestId, e.ValueDate, e.MaturityDate, e.Contract, e.RiskFreeRate);
                await eventApi.SendFuturesOptionTickDataStreamingStartedCompleteAsync(e);

                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"futures option {e.Contract.ContractId} streaming started");
                p.Logger.LogInformationEvent("{Source}: futures option {ContractId} streaming started", source, e.Contract.ContractId);
                return true;
            }
            else
                throw new InvalidOperationException("Market data API failed to start for unknown reasons.");
        }
        catch (Exception ex)
        {
            await eventApi.SendFuturesOptionTickDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, FuturesOptionTickDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming start failed", source, e.Contract.ContractId);
        }
        return false;
    }
}
