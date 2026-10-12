namespace TomasAI.IFM.Shared.Application;
public static class FuturesOptionTradeUriPath
{
    public const string Open = "/api/futures/option/trade/open";
    public const string Close = "/api/futures/option/trade/close";
    public const string Delete = "/api/futures/option/trade/delete";
    public const string DeleteOptionTrades = "/api/futures/option/trades/delete";
    public const string PlaceOrder = "/api/futures/option/trade/order/place";
    public const string ProcessEndOfDay = "/api/futures/option/trade/endofday/process";
    public const string ChangeDistributionStatistics = "/api/futures/option/trade/DistributionStatistics/change";
    public const string ChangeLegData = "/api/futures/option/trade/leg/data/change";
    public const string Snapshot = "/api/futures/option/trade/snapshot";
    public const string UpdateDailyProfitTarget = "/api/futures/option/trade/dailyprofittarget/update";
    public const string InsertSpreadData = "/api/futures/option/trade/spread/data/insert";
    public const string InsertSpreadBarData = "/api/futures/option/trade/spread/bardata/insert";
    public const string DeleteSpreadBarData = "/api/futures/option/trade/spread/bardata/delete";
}
