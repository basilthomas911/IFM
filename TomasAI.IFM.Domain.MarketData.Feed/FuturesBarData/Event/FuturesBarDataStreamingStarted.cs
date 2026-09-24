using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

/// <summary>Handles registration of the periodic futures-bar insertion callback.</summary>
public static class FuturesBarDataStreamingStarted
{
    static FuturesBarDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesBarDataEvent}";
    }
    static string ServiceId { get; } = default!;

    /// <summary>Starts the bar timer and publishes a correlated completion or failure event.</summary>
    public static async ValueTask<bool> ExecuteAsync(
    this FuturesBarDataStreamingStartedEvent e,
    IEventActorContext context,
    IEventActorContext commandApi,
    IEventActorContext eventApi,
    FuturesBarDataEventParameters p, ILogger<FuturesBarDataEventActor> logger)
    {
        var source = $"FuturesBarDataStreamingStartedEvent for EntityId: {e.EntityId}";
        var started = false;
        try
        {
            p.FuturesBarDataTimer.Start(e.EntityId, InsertFuturesBarDataFromTickDataAsync);
            await eventApi.FuturesBarDataStreamingStartedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, source);
            logger.LogInformationEvent(ServiceId, "{Source}", source);
            started = true;
        }
        catch (Exception ex)
        {
            logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data streaming start failed", source);
            await eventApi.FuturesBarDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
        }
        return started;

        /// <summary>Samples current futures ticks and submits supported bar insertions.</summary>
        async ValueTask InsertFuturesBarDataFromTickDataAsync()
        {
            try
            {
                foreach (var o in e.Contracts!)
                {
                    if (!p.MarketDataApi.TryGetLastTickPrice(o.ContractId, out var snapshot)
                        || !FuturesBarMarketPrice.TryResolve(
                            o.Symbol,
                            snapshot,
                            out var barPrice))
                        continue;

                    if (snapshot.AssetTypeId != AssetTypeId.Futures
                        || !StringComparer.Ordinal.Equals(snapshot.ContractId, o.ContractId)
                        || snapshot.ValueDate != e.ValueDate)
                    {
                        logger.LogInformationEvent(
                            ServiceId,
                            "{Source}: ignored mismatched hot-cache snapshot for {ContractId}",
                            source,
                            o.ContractId);
                        continue;
                    }

                    switch (o.Symbol)
                    {
                        case "ES":
                        case "VX":
                            await commandApi.InsertFuturesBarDataAsync(new FuturesBarDataReadModel(
                                contractId: o.ContractId,
                                symbol: o.Symbol,
                                valueDate: e.ValueDate,
                                barDate: DateTime.UtcNow,
                                barRateType: BarRateType.FifteenSeconds,
                                barValue: barPrice,
                                upTrendTrigger: 0,
                                downTrendTrigger: 0
                            ));
                            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Inserted Futures Bar Data {o.ContractId}");
                            logger.LogInformationEvent(ServiceId, "{Source}", $"inserted futures bar data {o.ContractId}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data insert failed", source);
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
            }
        }
    }
}
