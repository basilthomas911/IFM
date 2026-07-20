using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Application.Api.IntegrationTests.CommandApiResults;
using TomasAI.IFM.Shared.Application.Commands;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public static class MapCommandExtension
{
    public static void MapCommandApi(this WebApplication app)
    {
        // application command api paths...
        app.MapPost(ApplicationUriPath.Start, async (HttpResponse resp) => await ApplicationCommandApiResult.FromStartAsync(resp));
        app.MapPost(ApplicationUriPath.Shutdown, async (HttpResponse resp) => await ApplicationCommandApiResult.FromShutdownAsync(resp));

        // fund command api paths...
        app.MapPost(FundUriPath.Create, async (HttpResponse resp) => await FundCommandApiResult.FromCreateFundAsync(resp));
        app.MapPost(FundUriPath.AddOrderToFund, async (HttpResponse resp) => await FundCommandApiResult.FromAddOrderToFundAsync(resp));
        app.MapPost(FundUriPath.RemoveOrderFromFund, async (HttpResponse resp) => await FundCommandApiResult.FromRemoveOrderFromFundAsync(resp));
        app.MapPost(FundUriPath.AddTradeToFundOrder, async (HttpResponse resp) => await FundCommandApiResult.FromAddTradeToFundOrderAsync(resp));
        app.MapPost(FundUriPath.RemoveTradeFromFundOrder, async (HttpResponse resp) => await FundCommandApiResult.FromRemoveTradeFromFundOrderAsync(resp));
        app.MapPost(FundUriPath.CloseFundOrder, async (HttpResponse resp) => await FundCommandApiResult.FromCloseFundOrderAsync(resp));
        app.MapPost(FundUriPath.ChangeFundOrderTradeState, async (HttpResponse resp) => await FundCommandApiResult.FromChangeFundOrderTradeStateAsync(resp));
        app.MapPost(FundUriPath.GenerateFundMaxProfit, async (HttpResponse resp) => await FundCommandApiResult.FromGenerateFundMaxProfitAsync(resp));
        app.MapPost(FundTransactionUriPath.Create, async (HttpResponse resp) => await FundCommandApiResult.FromCreateFundTransactionAsync(resp));
        app.MapPost(FundTransactionUriPath.CreateTransactions, async (HttpResponse resp) => await FundCommandApiResult.FromCreateFundTransactionsAsync(resp));
        app.MapPost(FundTransactionUriPath.ProcessEndOfDay, async (HttpResponse resp) => await FundCommandApiResult.FromProcessEndOfDayFundTransactionAsync(resp));

        // market data analytics command api paths...
        app.MapPost(MarketDataAnalyticsUriPath.StartFuturesRsiSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromStartFuturesRsiSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.StopFuturesRsiSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromStopFuturesRsiSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesRsiSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesRsiSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesRsiDailySignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesRsiDailySignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.UpdateFuturesTradeSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromUpdateFuturesTradeSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesTdiSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesTdiSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesItiSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesItiSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.SetFuturesItiSignalHoldTrade, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromSetFuturesItiSignalHoldTradeAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.ClearFuturesItiSignalHoldTrade, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromClearFuturesItiSignalHoldTradeAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesAtrSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesAtrSignalAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesAtrSignalFromIntraDayData, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesAtrSignalFromIntraDayDataAsync(resp));
        app.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesAdxSignal, async (HttpResponse resp) => await MarketDataAnalyticsCommandApiResult.FromGenerateFuturesAdxSignalAsync(resp));

        // market data command api paths...
        app.MapPost(MarketDataUriPath.AddFuturesContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromAddFuturesContractAsync(resp));
        app.MapPost(MarketDataUriPath.ChangeFuturesContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromChangeFuturesContractAsync(resp));
        app.MapPost(MarketDataUriPath.RemoveFuturesContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromRemoveFuturesContractAsync(resp));
        app.MapPost(MarketDataUriPath.AddFuturesOptionContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromAddFuturesOptionContractAsync(resp));
        app.MapPost(MarketDataUriPath.AddFuturesOptionContracts, async (HttpResponse resp) => await MarketDataCommandApiResult.FromAddFuturesOptionContractsAsync(resp));
        app.MapPost(MarketDataUriPath.ChangeFuturesOptionContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromChangeFuturesOptionContractAsync(resp));
        app.MapPost(MarketDataUriPath.RemoveFuturesOptionContract, async (HttpResponse resp) => await MarketDataCommandApiResult.FromRemoveFuturesOptionContractAsync(resp));
        app.MapPost(MarketDataUriPath.AddYieldCurveRate, async (HttpResponse resp) => await MarketDataCommandApiResult.FromAddYieldCurveRateAsync(resp));
        app.MapPost(MarketDataUriPath.ChangeYieldCurveRate, async (HttpResponse resp) => await MarketDataCommandApiResult.FromChangeYieldCurveRateAsync(resp));
        app.MapPost(MarketDataUriPath.RemoveYieldCurveRate, async (HttpResponse resp) => await MarketDataCommandApiResult.FromRemoveYieldCurveRateAsync(resp));
        app.MapPost(MarketDataUriPath.ImportYieldCurveRates, async (HttpResponse resp) => await MarketDataCommandApiResult.FromImportYieldCurveRatesAsync(resp));

        // market data feed command api paths...
        app.MapPost(MarketDataFeedUriPath.StartMarketDataFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStartMarketDataFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StopMarketDataFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStopMarketDataFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.ResetMarketDataFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromResetMarketDataFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesEodData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesEodDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesTickData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesTickDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesOptionTickData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesOptionTickDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertVixFuturesEodData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertVixFuturesEodDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesBarData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesBarDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.DeleteFuturesBarData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromDeleteFuturesBarDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesOptionQuoteData, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesOptionQuoteDataAsync(resp));
        app.MapPost(MarketDataFeedUriPath.InsertFuturesClosingPrice, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromInsertFuturesClosingPriceAsync(resp));
        app.MapPost(MarketDataFeedUriPath.TurnTradeLiveFeedOn, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromEnableTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.TurnTradeLiveFeedOff, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromDisableTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.AddTradeLiveFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromAddTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.RemoveTradeLiveFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromRemoveTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.DeleteStreamingRequestId, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromDeleteStreamingRequestIdAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StartFuturesOptionTickDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStartFuturesOptionTickDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StopFuturesOptionTickDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStopFuturesOptionTickDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StartFuturesTickDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStartFuturesTickDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StopFuturesTickDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStopFuturesTickDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StartFuturesBarDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStartFuturesBarDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StopFuturesBarDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStopFuturesBarDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.EnableTradeLiveFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromEnableTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.DisableTradeLiveFeed, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromDisableTradeLiveFeedAsync(resp));
        app.MapPost(MarketDataFeedUriPath.RemoveTradeLiveFeeds, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromRemoveTradeLiveFeedsAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StartFuturesOptionQuoteDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStartFuturesOptionQuoteDataStreamingAsync(resp));
        app.MapPost(MarketDataFeedUriPath.StopFuturesOptionQuoteDataStreaming, async (HttpResponse resp) => await MarketDataFeedCommandApiResult.FromStopFuturesOptionQuoteDataStreamingAsync(resp));

        // option pricer command api paths...
        app.MapPost(OptionPricerUriPath.InsertSpreadDistribution, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromInsertSpreadDistributionAsync(resp));
        app.MapPost(OptionPricerUriPath.SubmitSpreadDistributionJob, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromSubmitSpreadDistributionJobAsync(resp));
        app.MapPost(OptionPricerUriPath.CompleteSpreadDistributionJob, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromCompleteSpreadDistributionJobAsync(resp));
        app.MapPost(OptionPricerUriPath.FailSpreadDistributionJob, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromFailSpreadDistributionJobAsync(resp));
        app.MapPost(OptionPricerUriPath.ClearSpreadDistributionJob, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromClearSpreadDistributionJobAsync(resp));
        app.MapPost(OptionPricerUriPath.DeleteSpreadDistributionJobsInProgress, async (HttpResponse resp) => await OptionPricerCommandApiResult.FromDeleteSpreadDistributionJobsInProgressAsync(resp));

        // option trade command api paths...
        app.MapPost(OptionTradeUriPath.Snapshot, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromSnapshotAsync(resp));
        app.MapPost(OptionTradeUriPath.Delete, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromDeleteAsync(resp));
        app.MapPost(OptionTradeUriPath.PlaceOrder, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromPlaceOrderAsync(resp));
        app.MapPost(OptionTradeUriPath.Open, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromOpenOptionTradeAsync(resp));
        app.MapPost(OptionTradeUriPath.Close, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromCloseOptionTradeAsync(resp));
        app.MapPost(OptionTradeUriPath.InsertSpreadData, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromInsertOptionTradeSpreadDataAsync(resp));
        app.MapPost(OptionTradeUriPath.InsertSpreadBarData, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromInsertOptionTradeSpreadBarDataAsync(resp));
        app.MapPost(OptionTradeUriPath.DeleteSpreadBarData, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromDeleteOptionTradeSpreadBarDataAsync(resp));
        app.MapPost(OptionTradeUriPath.ChangeLegData, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromChangeOptionLegDataAsync(resp));
        app.MapPost(OptionTradeUriPath.ChangeDistributionStatistics, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromChangeDistributionStatisticsAsync(resp));
        app.MapPost(OptionTradeUriPath.ProcessEndOfDay, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromProcessEndOfDayAsync(resp));
        app.MapPost(OptionTradeUriPath.UpdateDailyProfitTarget, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromUpdateTradeLimitDailyProfitTargetAsync(resp));
        app.MapPost(OptionTradeUriPath.DeleteOptionTrades, async (HttpResponse resp) => await OptionTradeCommandApiResult.FromDeleteOptionTradesAsync(resp));

        // reference command api paths...
        app.MapPost(ReferenceUriPath.ChangeEconomicCalendar, async (HttpResponse resp) => await ReferenceCommandApiResult.FromChangeEconomicCalendarAsync(resp));
        app.MapPost(ReferenceUriPath.AddEconomicCalendar, async (HttpResponse resp) => await ReferenceCommandApiResult.FromAddEconomicCalendarAsync(resp));
        app.MapPost(ReferenceUriPath.RemoveEconomicCalendar, async (HttpResponse resp) => await ReferenceCommandApiResult.FromRemoveEconomicCalendarAsync(resp));
        app.MapPost(ReferenceUriPath.ImportEconomicCalendars, async (HttpResponse resp) => await ReferenceCommandApiResult.FromImportEconomicCalendarsAsync(resp));
        app.MapPost(ReferenceUriPath.AddLookupType, async (HttpResponse resp) => await ReferenceCommandApiResult.FromAddLookupTypeAsync(resp));
        app.MapPost(ReferenceUriPath.ChangeLookupType, async (HttpResponse resp) => await ReferenceCommandApiResult.FromChangeLookupTypeAsync(resp));
        app.MapPost(ReferenceUriPath.RemoveLookupType, async (HttpResponse resp) => await ReferenceCommandApiResult.FromRemoveLookupTypeAsync(resp));

        // trade placement command api paths...
        app.MapPost(TradePlacementUriPath.Signal, async (HttpResponse resp) => await TradePlacementCommandApiResult.FromSignalTradePlacementAsync(resp));
        app.MapPost(TradePlacementUriPath.Start, async (HttpResponse resp) => await TradePlacementCommandApiResult.FromStartTradePlacementAsync(resp));
        app.MapPost(TradePlacementUriPath.Stop, async (HttpResponse resp) => await TradePlacementCommandApiResult.FromStopTradePlacementAsync(resp));

        // trade plan command api paths...
        app.MapPost(TradePlanUriPath.Update, async (HttpResponse resp) => await TradePlanCommandApiResult.FromUpdateTradePlanAsync(resp));
        app.MapPost(TradePlanUriPath.UpdateIronCondorTradePlan, async (HttpResponse resp) => await TradePlanCommandApiResult.FromUpdateIronCondorTradePlanAsync(resp));
        app.MapPost(TradePlanUriPath.UpdateForwardLossLimit, async (HttpResponse resp) => await TradePlanCommandApiResult.FromUpdateTradePlanForwardLossLimitAsync(resp));
        app.MapPost(TradePlanUriPath.ClearForwardLossLimit, async (HttpResponse resp) => await TradePlanCommandApiResult.FromClearTradePlanForwardLossLimitAsync(resp));

        // system admin command api paths...
        app.MapPost(SystemAdminUriPath.BackupDatabase, async (HttpResponse resp) => await SystemAdminCommandApiResult.FromBackupDatabaseAsync(resp));

    }
}
