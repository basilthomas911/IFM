using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public static class FuturesTickDataStreamingStarted
{
    static FuturesTickDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesTickDataStreamingStartedEvent e,  IEventActorContext context, FuturesTickDataEventParameters p)
    {
        var source = $"FuturesTickDataStreamingStartedEvent for EntityId: {e.EntityId}";
        try
        {
            var started = p.MarketDataApi.Start(async (errorCode, errorMsg) => await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, errorCode, errorMsg));
            if (started)
            {
                var streamId = p.MarketDataApi.StreamIds.Add(e.Contract.ContractId);
                if (streamId == -1)
                    throw new InvalidOperationException($"{e.GetType().Name}: unable to create stream id from futures contract {e.Contract.ContractId} ");
                p.MarketDataApi.StartStreamingFuturesTickData(streamId, e.ValueDate, e.Contract);
                p.BlackboardService.FuturesTickDataStreamingParameter.Set(streamId, new FuturesTickDataStreamingParameter(streamId, e.ValueDate, e.Contract));
                await context.FuturesTickDataStreamingStartedCompleteAsync(e);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, $"Futures {e.Contract.ContractId} streaming started");
                p.Logger.LogInformationEvent(ServiceId, "{Source}: futures {e.Contract.ContractId} streaming started", source, e.Contract.ContractId);
            }
            else
                throw new InvalidOperationException("Market data API failed to start for unknown reasons.");
            return true;
        }
        catch (Exception ex)
        {
            await context.FuturesTickDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, 6003, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures {e.Contract.ContractId} streaming start failed", source, e.Contract.ContractId);
        }
        return false;
    }
}
