namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying market data endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// market data API. These endpoints are used to retrieve market data such as contracts, quotes, rates, and analytics.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class MarketDataQueryUriPath
{
    public const string GetOnTheRunFuturesContract = "/api/marketdata/futures/on-the-run";
    public const string GetRolloverFuturesContracts = "/api/marketdata/futures/rollover-set";
    public const string GetFuturesContract = "/api/marketdata/futures/contract";
    public const string GetFuturesContractSymbol = "/api/marketdata/futures/contract/symbol";
    public const string GetFuturesTradeSignal = "/api/marketdata/futures/tradesignal";
    public const string GetFuturesOptionContract = "/api/marketdata/futures/option/contract";
    public const string GetFuturesContracts = "/api/marketdata/futures/contracts";
    public const string GetFuturesOptionContractsPage = "/api/marketdata/futures/option/contracts/page";
    public const string GetFuturesOptionContracts = "/api/marketdata/futures/option/contracts";
    public const string GetLastYieldCurveRate = "/api/marketdata/yieldcurve/last";
    public const string GetLastRateOfReturn = "/api/marketdata/rateofreturn/last";
    public const string GetTradingDays = "/api/marketdata/tradingdays";
    public const string GetTradingDates = "/api/marketdata/tradingdates";
    public const string GetYieldCurveRates = "/api/marketdata/yieldcurve/rates";
    public const string GetYieldCurveRateYears = "/api/marketdata/yieldcurve/years";
    public const string YieldCurveRateExists = "/api/marketdata/yieldcurve/exists";
    public const string GetValueDate = "/api/marketdata/valuedate";
    public const string GetMarketSession = "/api/marketdata/session";
    public const string GetTradeStrategySymbols = "/api/marketdata/trade-strategy-symbols";
    public const string GetInstrumentDefinitions = "/api/marketdata/instrument-definitions";
    public const string GetIronCondorMarketData = "/api/marketdata/ironcondor";
    public const string GetFuturesOptionContractIds = "/api/marketdata/futures/option/contractids";
    public const string GetEconomicCalendars = "/api/marketdata/economiccalendar";
    public const string GetEconomicCalendarPage = "/api/marketdata/economiccalendar/page";
    public const string GetEconomicCalendarAll = "/api/marketdata/economiccalendar/all";
    public const string GetEconomicCalendarDate = "/api/marketdata/economiccalendar/date";
    public const string GetEconomicCalendarCountryCodes = "/api/marketdata/economiccalendar/countrycodes";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
