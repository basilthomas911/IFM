namespace TomasAI.IFM.Shared.Application;
public static class FundUriPath
{
    public const string Create = "/api/fund/create";
    public const string AddOrderToFund = "/api/fund/order/add";
    public const string AddTradeToFundOrder = "/api/fund/order/trade/add";
    public const string ChangeFundOrderTradeState = "/api/fund/order/trade/state/change";
    public const string RemoveOrderFromFund = "/api/fund/order/remove";
    public const string RemoveTradeFromFundOrder = "/api/fund/order/trade/remove";
    public const string CloseFundOrder = "/api/fund/order/close";
    public const string GenerateFundMaxProfit = "/api/fund/maxprofit/generate";
}
