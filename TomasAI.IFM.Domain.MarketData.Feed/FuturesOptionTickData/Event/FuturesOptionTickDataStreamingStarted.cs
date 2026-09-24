using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

/// <summary>Handles the start of an option tick-data stream.</summary>
public static class FuturesOptionTickDataStreamingStarted
{
    /// <summary>Initializes the event service identity used for failure logging.</summary>
    static FuturesOptionTickDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesOptionTickDataEvent}";
    }

    static string ServiceId { get; }

    /// <summary>Attaches the option stream and publishes its completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesOptionTickDataStreamingStartedEvent e,
    IEventActorContext context,
    IEventActorContext eventApi,
    FuturesOptionTickDataEventParameters p, ILogger<FuturesOptionTickDataEventActor> logger)
    {
        var source = $"FuturesOptionTickDataStreamingStartedEvent for EntityId: {e.EntityId}";
        try
        {
            var runtime = p.MarketDataApi.GetRuntimeStatus();
            if (!runtime.IsRunning || runtime.ActiveValueDate != e.ValueDate)
                throw new InvalidOperationException(
                    "The Databento watchdog must start and qualify the value-date runtime before an option route is attached.");
            _ = await p.MarketDataApi.GetFuturesOptionContractAsync(
                e.Contract.ContractId)
                ?? throw new InvalidOperationException(
                    $"Futures option contract '{e.Contract.ContractId}' is not configured in the active market-data epoch.");
            var owner = CreateOwner(e.EntityId, e.Contract.ContractId);
            _ = await p.MarketDataApi.StartStreamingFuturesOptionTickDataAsync(
                e.Contract.ContractId,
                owner).ConfigureAwait(false);
            p.Streams.Track(owner, e.Contract.ContractId, e.Contract);
            await eventApi.SendFuturesOptionTickDataStreamingStartedCompleteAsync(e);

            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, $"futures option {e.Contract.ContractId} streaming started");
            logger.LogInformationEvent("{Source}: futures option {ContractId} streaming started", source, e.Contract.ContractId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures option {ContractId} streaming start failed", source, e.Contract.ContractId);
            await eventApi.SendFuturesOptionTickDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesOptionTickDataEvent, FuturesOptionTickDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
        }
        return false;
    }

    /// <summary>Creates the owner identity used to track this actor's option stream.</summary>
    internal static TickerStreamOwner CreateOwner(
        FuturesOptionTickEntityId entityId,
        string contractId) => new(
        nameof(FuturesOptionTickDataEventActor),
        entityId.Format(),
        contractId);
}
