namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI path constants for querying fund-related resources in the API.
/// </summary>
/// <remarks>This class contains predefined URI paths for various fund-related operations, such as retrieving fund
/// details, balances, transactions, and reports. These constants can be used to construct API requests in a consistent
/// and centralized manner.</remarks>
public class FundQueryUriPath
{
    public const string GetFunds = "/api/funds/";
    public const string GetFundOrders = "/api/fund/orders";
    public const string GetFundOrderTrades = "/api/fund/order/trades";
    public const string GetFundBalance  = "/api/fund/balance";
    public const string GetOpeningFundBalance = "/api/fund/balance/open";
    public const string GetClosingFundBalance = "/api/fund/balance/close";
    public const string GetFundTransactions = "/api/fund/transactions";
    public const string GetFundPnlReport = "/api/fund/pnlreport";
    public const string GetFundIdFromOrderId = "/api/fundid/from/orderid";
    public const string GetFundWinLossRatio = "/api/fund/winlossratio";
    public const string GetFundDrawdownBalances = "/api/fund/drawdown/balances";
}


/// <summary>
/// Provides a collection of URI paths for querying market data analytics related to futures trading signals.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// market data analytics API. These endpoints are used to retrieve different types of futures trading signals,
/// including RSI signals, ITI signals, and trend direction data. The URIs are intended to be used as part of HTTP
/// requests to the API.</remarks>
public class MarketDataAnalyticsQueryUriPath
{
    public const string GetFuturesTradeSignal = "/api/marketdata/analytics/futures/tradesignal";
    public const string GetLastFuturesTradeSignal = "/api/marketdata/analytics/futures/tradesignal/last";
    public const string GetFuturesTradeSignalBySymbol = "/api/marketdata/analytics/futures/tradesignal/bysymbol";
    public const string GetFuturesTradeSignalIds = "/api/marketdata/analytics/futures/tradesignal/ids";
    public const string GetFuturesRsiSignal = "/api/marketdata/analytics/futures/rsisignal";
    public const string GetFuturesTrendDirectionFromRSISignal = "/api/marketdata/analytics/futures/rsisignal/trenddirection";
    public const string GetFuturesTdiSignal = "/api/marketdata/analytics/futures/tdisignal";
    public const string GetFuturesItiSignal = "/api/marketdata/analytics/futures/itisignal";
    public const string GetFuturesItiTrendDirectionChangedSignals = "/api/marketdata/analytics/futures/ititrend/directionchangedsignals";
    public const string GetFuturesItiSignalData = "/api/marketdata/analytics/futures/itisignaldata";
    public const string GetFuturesItiMDIDistribution = "/api/marketdata/analytics/futures/itimdidistribution";
    public const string GetFuturesItiMDIDistributionByTrend = "/api/marketdata/analytics/futures/itimdidistribution/bytrend";
    public const string GetFuturesItiSignalMDI = "/api/marketdata/analytics/futures/itisignal/mdi";
    public const string GetFuturesItiSignalMDIByTrend = "/api/marketdata/analytics/futures/itisignal/mdi/bytrend";
    public const string GetFuturesAtrSignal = "/api/marketdata/analytics/futures/atrsignal";
    public const string GetFuturesAdxSignal = "/api/marketdata/analytics/futures/adxsignal";
    public const string GetFuturesMacdSignal = "/api/marketdata/analytics/futures/macdsignal";
}

/// <summary>
/// Provides a collection of URI paths for querying market data feed endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// market data feed API. These endpoints are used to retrieve futures tick data, EOD data, bar data, option contracts,
/// and related analytics. The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class MarketDataFeedQueryUriPath
{
    public const string GetLastFuturesTickData = "/api/marketdata/feed/futures/lasttick";
    public const string GetLastFuturesTickDataByTickDate = "/api/marketdata/feed/futures/lasttick/bydate";
    public const string GetLastFuturesOptionTickData = "/api/marketdata/feed/futures/option/lasttick";
    public const string GetFuturesEodData = "/api/marketdata/feed/futures/eod";
    public const string GetLastFuturesEodData = "/api/marketdata/feed/futures/eod/last";
    public const string GetFuturesEodDataByDateRange = "/api/marketdata/feed/futures/eod/daterange";
    public const string GetFuturesBarData = "/api/marketdata/feed/futures/bar";
    public const string GetLastFuturesBarData = "/api/marketdata/feed/futures/bar/last";
    public const string GetIronCondorMarketDataFeed = "/api/marketdata/feed/ironcondor";
    public const string GetFuturesEodDataParameters = "/api/marketdata/feed/futures/eod/parameters";
    public const string GetFuturesOptionContract = "/api/marketdata/feed/futures/option/contract";
    public const string GetFuturesOptionSpreadData = "/api/marketdata/feed/futures/option/spread";
    public const string GetNormalCurveTable = "/api/marketdata/feed/normalcurvetable";
    public const string GetVixFuturesEodData = "/api/marketdata/feed/vixfutures/eod";
    public const string GetLastVixFuturesEodData = "/api/marketdata/feed/vixfutures/eod/last";
    public const string GetFuturesRiskPositionType = "/api/marketdata/feed/futures/riskpositiontype";
    public const string GetFuturesEodMovingAverages = "/api/marketdata/feed/futures/eod/movingaverages";
    public const string GetStreamingRequestId = "/api/marketdata/feed/streaming/requestid";
    public const string GetOptionQuoteId = "/api/marketdata/feed/option/quoteid";
}

public class FuturesBarDataQueryUriPath
{

}

    /// <summary>
    /// Provides a collection of URI paths for querying market data endpoints.
    /// </summary>
    /// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
    /// market data API. These endpoints are used to retrieve market data such as contracts, quotes, rates, and analytics.
    /// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
    public class MarketDataQueryUriPath
{
    public const string GetCurrentlyTradedFuturesContract = "/api/marketdata/futures/currentlytraded";
    public const string GetCurrentlyTradedFuturesContracts = "/api/marketdata/futures/currentlytraded/all";
    public const string GetFuturesContract = "/api/marketdata/futures/contract";
    public const string GetFuturesContractSymbol = "/api/marketdata/futures/contract/symbol";
    public const string GetFuturesTradeSignal = "/api/marketdata/futures/tradesignal";
    public const string GetFuturesOptionContract = "/api/marketdata/futures/option/contract";
    public const string GetFuturesContracts = "/api/marketdata/futures/contracts";
    public const string GetFuturesOptionContracts = "/api/marketdata/futures/option/contracts";
    public const string GetLastYieldCurveRate = "/api/marketdata/yieldcurve/last";
    public const string GetLastRateOfReturn = "/api/marketdata/rateofreturn/last";
    public const string GetTradingDays = "/api/marketdata/tradingdays";
    public const string GetTradingDates = "/api/marketdata/tradingdates";
    public const string GetYieldCurveRates = "/api/marketdata/yieldcurve/rates";
    public const string GetYieldCurveRateYears = "/api/marketdata/yieldcurve/years";
    public const string YieldCurveRateExists = "/api/marketdata/yieldcurve/exists";
    public const string GetValueDate = "/api/marketdata/valuedate";
    public const string GetExternalYieldCurveRates = "/api/marketdata/yieldcurve/externalrates";
    public const string GetIronCondorMarketData = "/api/marketdata/ironcondor";
    public const string GetFuturesOptionContractIds = "/api/marketdata/futures/option/contractids";
}

/// <summary>
/// Provides a collection of URI paths for querying option pricer endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// option pricer API. These endpoints are used to retrieve option pricer devices, spread distributions, and job status.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class OptionPricerQueryUriPath
{
    public const string GetOptionPricerDevices = "/api/optionpricer/devices";
    public const string GetSpreadDistribution = "/api/optionpricer/spreaddistribution";
    public const string IsSpreadDistributionJobInProgress = "/api/optionpricer/spreaddistribution/jobinprogress";
}

/// <summary>
/// Provides a collection of URI paths for querying reference data endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// reference API. These endpoints are used to retrieve lookup types, economic calendars, seed IDs, and related reference data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class ReferenceQueryUriPath
{
    public const string GetDefaultFuturesContractDefinitions = "/api/reference/defaultfuturescontractdefinitions";
    public const string GetLookupTypes = "/api/reference/lookuptypes";
    public const string GetLookupType = "/api/reference/lookuptype";
    public const string GetLookupTypeNames = "/api/reference/lookuptype/names";
    public const string GetLookupTypeShortCodes = "/api/reference/lookuptype/shortcodes";
    public const string LookupTypeShortCodeExists = "/api/reference/lookuptype/shortcodeexists";
    public const string GetMarketDataDefinitionTypes = "/api/reference/marketdatadefinitiontypes";
    public const string GetReferenceDataDefinitionTypes = "/api/reference/referencedatadefinitiontypes";
    public const string GetSystemAdminFunctionTypes = "/api/reference/systemadminfunctiontypes";
    public const string GetNextSeedId = "/api/reference/nextseedid";
    public const string GetCurrentSeedId = "/api/reference/currentseedid";
    public const string GetFuturesOptionStrikePriceDefinitions = "/api/reference/futuresoptionstrikepricedefinitions";
    public const string GetEconomicCalendars = "/api/reference/economiccalendar";
    public const string GetEconomicCalendarAll = "/api/reference/economiccalendar/all";
    public const string GetExternalEconomicCalendars = "/api/reference/external/economiccalendars";
    public const string GetEconomicCalendarDate = "/api/reference/economiccalendar/date";
    public const string GetEconomicCalendarCountryCodes = "/api/reference/economiccalendar/countrycodes";
    public const string GetMDIForwardLossRatios = "/api/reference/mdiforwardlossratios";
}

/// <summary>
/// Provides a collection of URI paths for querying trade plan endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// trade plan API. These endpoints are used to retrieve loss probabilities, trade plan actions, forward loss ratios, and related data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class TradePlanQueryUriPath
{
    public const string GetLossProbability = "/api/tradeplan/lossprobability";
    public const string GetLossProbabilityDistribution = "/api/tradeplan/lossprobabilitydistribution";
    public const string GetStopLossLimit = "/api/tradeplan/stoplosslimit";
    public const string GetTradePlanForwardLossRatios = "/api/tradeplan/forwardlossratios";
    public const string GetTradePlanForwardLossRatio = "/api/tradeplan/forwardlossratio";
    public const string GetTradePlanAction = "/api/tradeplan/action";
    public const string GetTradePlans = "/api/tradeplan/tradeplans";
    public const string GetIronCondorForwardDelta = "/api/tradeplan/ironcondorforwarddelta";
    public const string GetTradePlanForwardLossLimit = "/api/tradeplan/forwardlosslimit";
    public const string GetTradePlanForwardLossLimitType = "/api/tradeplan/forwardlosslimit/type";
}

/// <summary>
/// Provides a collection of URI paths for querying trade endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// trade API. These endpoints are used to retrieve trade history, option trades, trade limits, positions, and related data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class TradeQueryUriPath
{
    public const string GetTradeHistory = "/api/trade/history";
    public const string GetOptionLegContractIds = "/api/trade/optionlegcontractids";
    public const string GetTradeLimit = "/api/trade/tradelimit";
    public const string GetTradeTypeLimit = "/api/trade/tradetypelimit";
    public const string GetTradeQuantity = "/api/trade/tradequantity";
    public const string GetOptionTrade = "/api/trade/optiontrade";
    public const string GetOptionTrades = "/api/trade/optiontrades";
    public const string GetOptionTradeSpreadData = "/api/trade/optiontradespreaddata";
    public const string GetOptionTradeSpreadBarData = "/api/trade/optiontradespreadbardata";
    public const string GetTradePositions = "/api/trade/tradepositions";
    public const string GetTradePosition = "/api/trade/tradeposition";
    public const string GetIronCondorTradePrice = "/api/trade/ironcondortradeprice";
    public const string GetTradePlanSummary = "/api/trade/tradeplansummary";
    public const string GetTradePositionTradeTypes = "/api/trade/tradepositiontradetypes";
    public const string GetIronCondorMDILimit = "/api/trade/ironcondormdilimit";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// system administrator API. These endpoints are used to retrieve database names and other administrative functions.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public class SystemAdminQueryUriPath
{
    public const string GetDatabaseNames = "/api/systemadmin/databasenames";
}
