using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

/// <summary>Handles the stop of an option tick-data stream.</summary>
public static class FuturesOptionTickDataStreamingStopped
{
    /// <summary>Initializes the event service identity used for failure logging.</summary>
    static FuturesOptionTickDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

    /// <summary>Detaches the option stream and publishes its completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStoppedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesOptionTickDataEventParameters p, ILogger<FuturesOptionTickDataEventActor> logger)
    {
        var source = $"FuturesOptionTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            var owner = FuturesOptionTickDataStreamingStarted.CreateOwner(
                e.EntityId,
                e.ContractId);
            _ = await p.MarketDataApi.StopStreamingFuturesOptionTickDataAsync(
                e.ContractId,
                owner).ConfigureAwait(false);
            p.Streams.Untrack(e.ContractId, owner);
            await eventApi.SendFuturesOptionTickDataStreamingStoppedCompleteAsync(e);

            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"{e.ContractId} Streaming Stopped");
            logger.LogInformationEvent("{Source}: futures option {ContractId} streaming stopped", source, e.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming stop failed", source, e.ContractId);
            await eventApi.SendFuturesOptionTickDataStreamingStoppedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, 6008, ex.GetErrorMessage());
        }
        return false;
    }
}
