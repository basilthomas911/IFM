namespace TomasAI.IFM.Shared.Application;

public static class MarketDataUriPath
{
    public const string AddFuturesContract = "/api/marketdata/futures/contract/add";
    public const string ChangeFuturesContract = "/api/marketdata/futures/contract/change";
    public const string UpdateFuturesContract = "/api/marketdata/futures/contract/update";
    public const string RemoveFuturesContract = "/api/marketdata/futures/contract/remove";
    public const string AddFuturesOptionContract = "/api/marketdata/futures/option/contract/add";
    public const string AddFuturesOptionContracts = "/api/marketdata/futures/option/contracts/add";
    public const string ChangeFuturesOptionContract = "/api/marketdata/futures/option/contract/change";
    public const string RemoveFuturesOptionContract = "/api/marketdata/futures/option/contract/remove";
    public const string AddYieldCurveRate = "/api/marketdata/yieldcurverate/add";
    public const string ChangeYieldCurveRate = "/api/marketdata/yieldcurverate/change";
    public const string RemoveYieldCurveRate = "/api/marketdata/yieldcurverate/remove";
    public const string ImportYieldCurveRates = "/api/marketdata/yieldcurverates/import";
    public const string ChangeEconomicCalendar = "/api/marketdata/economiccalendar/change";
    public const string RemoveEconomicCalendar = "/api/marketdata/economiccalendar/remove";
    public const string AddEconomicCalendar = "/api/marketdata/economiccalendar/add";
    public const string ImportEconomicCalendars = "/api/marketdata/economiccalendars/import";

}
