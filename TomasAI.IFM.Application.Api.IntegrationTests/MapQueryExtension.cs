using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TomasAI.IFM.Application.Api.IntegrationTests.QueryApiResults;
using TomasAI.IFM.Shared.Application;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public static  class MapQueryExtension
{
    public static void MapQueryApi(this WebApplication app)
    {
        // map Fund Query APIs
        app.MapGet(FundQueryUriPath.GetFunds, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundsAsync(resp));
        app.MapGet(FundQueryUriPath.GetClosingFundBalance, async (HttpResponse resp) => await FundQueryApiResult.FromGetClosingFundBalanceAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundBalance, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundBalanceAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundDrawdownBalances, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundDrawdownBalancesAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundIdFromOrderId, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundIdFromOrderIdAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundOrders, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundOrdersAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundOrderTrades, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundOrderTradesAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundPnlReport, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundPnlReportAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundTransactions, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundTransactionsAsync(resp));
        app.MapGet(FundQueryUriPath.GetFundWinLossRatio, async (HttpResponse resp) => await FundQueryApiResult.FromGetFundWinLossRatioAsync(resp));
        app.MapGet(FundQueryUriPath.GetOpeningFundBalance, async (HttpResponse resp) => await FundQueryApiResult.FromGetOpeningFundBalanceAsync(resp));

        // map Market Data Analytics Query APIs
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesTradeSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetLastFuturesTradeSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetLastFuturesTradeSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignalBySymbol, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesTradeSignalBySymbolAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesTradeSignalIds, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesTradeSignalIdsAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesRsiSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesRsiSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesTrendDirectionFromRSISignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesTrendDirectionFromRSISignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesTdiSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesTdiSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiTrendDirectionChangedSignals, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiTrendDirectionChangedSignalsAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalData, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiSignalDataAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiMDIDistribution, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiMDIDistributionAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiMDIDistributionByTrend, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiMDIDistributionByTrendAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDI, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiSignalMDIAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesItiSignalMDIByTrend, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesItiSignalMDIByTrendAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesAtrSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesAtrSignalAsync(resp));
        app.MapGet(MarketDataAnalyticsQueryUriPath.GetFuturesAdxSignal, async (HttpResponse resp) => await MarketDataAnalyticsQueryApiResult.FromGetFuturesAdxSignalAsync(resp));

        // map Market Data Feed Query APIs
        app.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesTickData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastFuturesTickDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesTickDataByTickDate, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastFuturesTickDataByTickDateAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesOptionTickData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastFuturesOptionTickDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesEodDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesEodData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastFuturesEodDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodDataByDateRange, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesEodDataByDateRangeAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesBarData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesBarDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetLastFuturesBarData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastFuturesBarDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetIronCondorMarketDataFeed, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetIronCondorMarketDataFeedAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodDataParameters, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesEodDataParametersAsync(resp));
        app.MapPost(MarketDataFeedQueryUriPath.GetFuturesOptionContract, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesOptionContractAsync(resp));
        app.MapPost(MarketDataFeedQueryUriPath.GetFuturesOptionSpreadData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesOptionSpreadDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetNormalCurveTable, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetNormalCurveTableAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetVixFuturesEodData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetVixFuturesEodDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetLastVixFuturesEodData, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetLastVixFuturesEodDataAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesRiskPositionType, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesRiskPositionTypeAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetFuturesEodMovingAverages, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetFuturesEodMovingAveragesAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetStreamingRequestId, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetStreamingRequestIdAsync(resp));
        app.MapGet(MarketDataFeedQueryUriPath.GetOptionQuoteId, async (HttpResponse resp) => await MarketDataFeedQueryApiResult.FromGetOptionQuoteIdAsync(resp));

        // map Market Data Query APIs
        app.MapGet(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContract, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetCurrentlyTradedFuturesContractAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContracts, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetCurrentlyTradedFuturesContractsAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesContract, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesContractAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesContractSymbol, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesContractSymbolAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesTradeSignal, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesTradeSignalAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesOptionContract, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesOptionContractAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesContracts, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesContractsAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetFuturesOptionContracts, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesOptionContractsAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetLastYieldCurveRate, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetLastYieldCurveRateAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetLastRateOfReturn, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetLastRateOfReturnAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetTradingDays, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetTradingDaysAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetTradingDates, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetTradingDatesAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetYieldCurveRates, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetYieldCurveRatesAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetYieldCurveRateYears, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetYieldCurveRateYearsAsync(resp));
        app.MapGet(MarketDataQueryUriPath.YieldCurveRateExists, async (HttpResponse resp) => await MarketDataQueryApiResult.FromYieldCurveRateExistsAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetValueDate, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetValueDateAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetExternalYieldCurveRates, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetExternalYieldCurveRatesAsync(resp));
        app.MapGet(MarketDataQueryUriPath.GetIronCondorMarketData, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetIronCondorMarketDataAsync(resp));
        app.MapPost(MarketDataQueryUriPath.GetFuturesOptionContractIds, async (HttpResponse resp) => await MarketDataQueryApiResult.FromGetFuturesOptionContractIdsAsync(resp));

        // map Option Pricer Query APIs
        app.MapGet(OptionPricerQueryUriPath.GetOptionPricerDevices, async (HttpResponse resp) => await OptionPricerQueryApiResult.FromGetOptionPricerDevicesAsync(resp));
        app.MapGet(OptionPricerQueryUriPath.GetSpreadDistribution, async (HttpResponse resp) => await OptionPricerQueryApiResult.FromGetSpreadDistributionAsync(resp));
        app.MapGet(OptionPricerQueryUriPath.IsSpreadDistributionJobInProgress, async (HttpResponse resp) => await OptionPricerQueryApiResult.FromIsSpreadDistributionJobInProgressAsync(resp));

        // map Reference Query APIs
        app.MapGet(ReferenceQueryUriPath.GetDefaultFuturesContractDefinitions, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetDefaultFuturesContractDefinitionsAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetLookupTypes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetLookupTypesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetLookupType, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetLookupTypeAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetLookupTypeNames, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetLookupTypeNamesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetLookupTypeShortCodes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetLookupTypeShortCodesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetMarketDataDefinitionTypes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetMarketDataDefinitionTypesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetReferenceDataDefinitionTypes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetReferenceDataDefinitionTypesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetSystemAdminFunctionTypes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetSystemAdminFunctionTypesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetNextSeedId, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetNextSeedIdAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetCurrentSeedId, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetCurrentSeedIdAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetFuturesOptionStrikePriceDefinitions, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetFuturesOptionStrikePriceDefinitionsAsync(resp));
        app.MapGet(ReferenceQueryUriPath.LookupTypeShortCodeExists, async (HttpResponse resp) => await ReferenceQueryApiResult.FromLookupTypeShortCodeExistsAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetEconomicCalendars, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetEconomicCalendarsAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetEconomicCalendarAll, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetEconomicCalendarAllAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetExternalEconomicCalendars, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetExternalEconomicCalendarsAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetEconomicCalendarDate, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetEconomicCalendarDateAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetEconomicCalendarCountryCodes, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetEconomicCalendarCountryCodesAsync(resp));
        app.MapGet(ReferenceQueryUriPath.GetMDIForwardLossRatios, async (HttpResponse resp) => await ReferenceQueryApiResult.FromGetMDIForwardLossRatiosAsync(resp));

        // map System Admin Query APIs
        app.MapGet(SystemAdminQueryUriPath.GetDatabaseNames, async (HttpResponse resp) => await SystemAdminQueryApiResult.FromGetDatabaseNamesAsync(resp));

        // map Trade Plan Query APIs
        app.MapGet(TradePlanQueryUriPath.GetLossProbability, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetLossProbabilityAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetLossProbabilityDistribution, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetLossProbabilityDistributionAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetStopLossLimit, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetStopLossLimitAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetTradePlanForwardLossRatios, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetTradePlanForwardLossRatiosAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetTradePlanForwardLossRatio, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetTradePlanForwardLossRatioAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetTradePlanAction, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetTradePlanActionAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetTradePlans, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetTradePlansAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetIronCondorForwardDelta, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetIronCondorForwardDeltaAsync(resp));
        app.MapGet(TradePlanQueryUriPath.GetTradePlanForwardLossLimit, async (HttpResponse resp) => await TradePlanQueryApiResult.FromGetTradePlanForwardLossLimitAsync(resp));

        // map Trade Query APIs
        app.MapGet(TradeQueryUriPath.GetTradeHistory, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradeHistoryAsync(resp));
        app.MapGet(TradeQueryUriPath.GetOptionLegContractIds, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetOptionLegContractIdsAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradeLimit, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradeLimitAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradeTypeLimit, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradeTypeLimitAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradeQuantity, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradeQuantityAsync(resp));
        app.MapGet(TradeQueryUriPath.GetOptionTrade, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetOptionTradeAsync(resp));
        app.MapGet(TradeQueryUriPath.GetOptionTrades, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetOptionTradesAsync(resp));
        app.MapPost(TradeQueryUriPath.GetOptionTradeSpreadData, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetOptionTradeSpreadDataAsync(resp));
        app.MapPost(TradeQueryUriPath.GetOptionTradeSpreadBarData, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetOptionTradeSpreadBarDataAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradePositions, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradePositionsAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradePosition, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradePositionAsync(resp));
        app.MapGet(TradeQueryUriPath.GetIronCondorTradePrice, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetIronCondorTradePriceAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradePlanSummary, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradePlanSummaryAsync(resp));
        app.MapGet(TradeQueryUriPath.GetTradePositionTradeTypes, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetTradePositionTradeTypesAsync(resp));
        app.MapGet(TradeQueryUriPath.GetIronCondorMDILimit, async (HttpResponse resp) => await OptionTradeQueryApiResult.FromGetIronCondorMDILimitAsync(resp));


    }
}
