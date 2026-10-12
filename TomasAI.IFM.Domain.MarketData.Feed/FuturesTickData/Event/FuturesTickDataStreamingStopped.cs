using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

/// <summary>Handles the stop of a futures tick-data stream.</summary>
public static class FuturesTickDataStreamingStopped
{
    /// <summary>Initializes the event service identity used for failure logging.</summary>
    static FuturesTickDataStreamingStopped()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <summary>Detaches the futures stream and publishes its completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesTickDataStreamingStoppedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesTickDataEventParameters p, ILogger<FuturesTickDataEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(eventApi);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"FuturesTickDataStreamingStoppedEvent for EntityId: {e.EntityId}";
        try
        {
            if (p.MarketDataApi.CoreFuturesRoutesAreRuntimeOwned)
            {
                await (eventApi.FuturesTickDataStreamingStoppedCompleteAsync(e)).ConfigureAwait(false);
                logger.LogInformationEvent(ServiceId,
                    "{Source}: futures tick route {ContractId} remains owned by the supervised dataset worker",
                    source, e.ContractId);
                return true;
            }
            var owner = FuturesTickDataStreamingStarted.CreateOwner(e.EntityId, e.ContractId);
            _ = await p.MarketDataApi.StopStreamingFuturesTickDataAsync(
                e.ContractId,
                owner).ConfigureAwait(false);
            p.Streams.Untrack(e.ContractId, owner);
            await (eventApi.FuturesTickDataStreamingStoppedCompleteAsync(e)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, $"futures tick data {e.ContractId} streaming stopped")).ConfigureAwait(false);
            logger.LogInformationEvent(ServiceId, "{Source}: futures tick data {e.ContractId} streaming stopped", source, e.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures tick data {e.ContractId} streaming stop failed", source, e.ContractId);
            await (eventApi.FuturesTickDataStreamingStoppedFailAsync(e, ex)).ConfigureAwait(false);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesTickDataEvent, 6005, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }

}
