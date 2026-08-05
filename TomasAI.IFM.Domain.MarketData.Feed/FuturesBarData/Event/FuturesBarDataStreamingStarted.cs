using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
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
                    var futuresTickData = await context.GetLastFuturesTickDataAsync(o.ContractId, e.ValueDate);
                    if (futuresTickData is not null)
                    {
                        switch (o.Symbol)
                        {
                            case "ES":
                            case "VX":
                                var futuresTradeSignal = await context.GetLastFuturesTradeSignalAsync(o.Symbol, e.ValueDate);
                                await commandApi.InsertFuturesBarDataAsync(new FuturesBarDataReadModel(
                                    contractId: o.ContractId,
                                    symbol: o.Symbol,
                                    valueDate: e.ValueDate,
                                    barDate: DateTime.Now,
                                    barRateType: BarRateType.Minute,
                                    barValue: Convert.ToDecimal(futuresTickData?.Price),
                                    upTrendTrigger: futuresTradeSignal?.UpTrendingTrigger ?? 0,
                                    downTrendTrigger: futuresTradeSignal?.DownTrendingTrigger ?? 0
                                ));
                                await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, $"Inserted Futures Bar Data {o.ContractId}");
                                p.Logger.LogInformationEvent(ServiceId, "{Source}", $"inserted futures bar data {o.ContractId}");
                                break;
                        }
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
