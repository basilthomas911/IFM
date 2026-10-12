using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

/// <summary>Handles the MarketDataFeedStartedCompleteEvent message in the MarketDataFeedEventActor lifecycle.</summary>
public static class MarketDataFeedStartedComplete
{
    static MarketDataFeedStartedComplete()
    {
        ServiceId = $"{LogSourceType.MarketDataFeedEvent}";
    }

    static string ServiceId { get; } 
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="p"></param>
    /// <returns></returns>
    public static async ValueTask<bool> ExecuteAsync(
        this MarketDataFeedStartedCompleteEvent e,
        IEventActorContext context,
        IEventActorContext commandApi,
        MarketDataFeedEventParameters p, ILogger<MarketDataFeedEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(p);
        var source = $"MarketDataFeedStartedCompleteEvent for EntityId: {e.EntityId}";
        try
        {
            foreach (var futuresContract in e.FuturesContracts!)
            {
                await (p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent,
                    $"Starting to stream Futures {futuresContract.ContractId}...")).ConfigureAwait(false);
                logger.LogInformationEvent(
                    ServiceId,
                    "{Source}: starting to stream Futures {ContractId}...",
                    source,
                    futuresContract.ContractId);
                var entityId = new FuturesDataId(futuresContract.ContractId, e.ValueDate);
                await (commandApi.StartFuturesTickDataStreamingAsync(e, futuresContract, entityId)).ConfigureAwait(false);
                await (p.StatusConsoleWriter.WriteConsoleAsync(
                    LogSourceType.MarketDataFeedEvent,
                    $"Streaming Futures {futuresContract.ContractId} started")).ConfigureAwait(false);
                logger.LogInformationEvent(
                    ServiceId,
                    "{Source}: streaming Futures {ContractId} started",
                    source,
                    futuresContract.ContractId);
            }

            await (commandApi.StartFuturesBarDataStreamingAsync(
                e,
                new FuturesBarDataStreamingId(e.ValueDate))).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: market data feed start failed", source);
            await (p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, -1, ex.GetErrorMessage())).ConfigureAwait(false);
        }
        return false;
    }
}
