namespace TomasAI.IFM.Shared.Application;

public static class OptionTradeUriPath
{
    public const string Open = "/api/option/trade/open";
    public const string Close = "/api/option/trade/close";
    public const string Delete = "/api/option/trade/delete";
    public const string DeleteOptionTrades = "/api/option/trades/delete";
    public const string PlaceOrder = "/api/option/trade/order/place";
    public const string ProcessEndOfDay = "/api/option/trade/endofday/process";
    public const string ChangeDistributionStatistics = "/api/option/trade/DistributionStatistics/change";
    public const string ChangeLegData = "/api/option/trade/leg/data/change";
    public const string Snapshot = "/api/option/trade/snapshot";
    public const string UpdateDailyProfitTarget = "/api/option/trade/dailyprofittarget/update";
    public const string InsertSpreadData = "/api/option/trade/spread/data/insert";
    public const string InsertSpreadBarData = "/api/option/trade/spread/bardata/insert";
    public const string DeleteSpreadBarData = "/api/option/trade/spread/bardata/delete";
}
