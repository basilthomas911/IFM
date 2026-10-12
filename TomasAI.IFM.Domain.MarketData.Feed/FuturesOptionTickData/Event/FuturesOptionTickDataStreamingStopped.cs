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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Detaches the option stream and publishes its completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStoppedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesOptionTickDataEventParameters p, ILogger<FuturesOptionTickDataEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"FuturesOptionTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            var owner = FuturesOptionTickDataStreamingStarted.CreateOwner(
                e.EntityId,
                e.ContractId);
            if (p.QualifiedFeeds is { } feeds)
                await feeds.ReleaseAsync(owner, e.ContractId).ConfigureAwait(false);
            else
                _ = await p.MarketDataApi.StopStreamingFuturesOptionTickDataAsync(e.ContractId, owner).ConfigureAwait(false);
            p.Streams.Untrack(e.ContractId, owner);
            await (eventApi.SendFuturesOptionTickDataStreamingStoppedCompleteAsync(e)).ConfigureAwait(false);

            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"{e.ContractId} Streaming Stopped")).ConfigureAwait(false);
            logger.LogInformationEvent("{Source}: futures option {ContractId} streaming stopped", source, e.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming stop failed", source, e.ContractId);
            await (eventApi.SendFuturesOptionTickDataStreamingStoppedFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, 6008, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
