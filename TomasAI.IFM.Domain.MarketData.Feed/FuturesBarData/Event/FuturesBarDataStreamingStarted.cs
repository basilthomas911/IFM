using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

public static class FuturesBarDataStreamingStarted
{
    static FuturesBarDataStreamingStarted()
    {
        ServiceId = $"{LogSourceType.FuturesBarDataEvent}";
    }
    static string ServiceId { get; } = default!;

   /// <summary>
   /// 
   /// </summary>
   /// <param name="e"></param>
   /// <param name="context"></param>
   /// <param name="p"></param>
   /// <returns></returns>
public static async ValueTask<bool> ExecuteAsync(
    this FuturesBarDataStreamingStartedEvent e,
    IEventActorContext context,
    IActorMarketDataFeedCommandApi commandApi,
    IActorMarketDataFeedEventApi eventApi,
    FuturesBarDataEventParameters p)
    {
        var source = $"FuturesBarDataStreamingStartedEvent for EntityId: {e.EntityId}";
        var started = false;
        try
        {
            p.FuturesBarDataTimer.Start(e.EntityId, InsertFuturesBarDataFromTickDataAsync);
            await eventApi.FuturesBarDataStreamingStartedCompleteAsync(e);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, source);
            p.Logger.LogInformationEvent(ServiceId, "{Source}", source);
            started = true;
        }
        catch (Exception ex)
        {
            await eventApi.FuturesBarDataStreamingStartedFailAsync(e, ex);
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data streaming start failed", source);
        }
        return started;

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
                        p.Logger.LogInformationEvent(
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
                            p.Logger.LogInformationEvent(ServiceId, "{Source}", $"inserted futures bar data {o.ContractId}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, FuturesBarDataStreamingStartedEvent.ErrorCode, ex.GetErrorMessage());
                p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures bar data insert failed", source);
            }
        }
    }
}
