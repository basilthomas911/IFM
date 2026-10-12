namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI path constants for querying fund-related resources in the API.
/// </summary>
/// <remarks>This class contains predefined URI paths for various fund-related operations, such as retrieving fund
/// details, balances, transactions, and reports. These constants can be used to construct API requests in a consistent
/// and centralized manner.</remarks>
public static class FundQueryUriPath
{
    public const string GetFunds = "/api/funds/";
    public const string GetFundOrders = "/api/fund/orders";
    public const string GetFundOrderTrades = "/api/fund/order/trades";
    public const string GetFundBalance = "/api/fund/balance";
    public const string GetOpeningFundBalance = "/api/fund/balance/open";
    public const string GetClosingFundBalance = "/api/fund/balance/close";
    public const string GetFundTransactions = "/api/fund/transactions";
    public const string GetFundPnlReport = "/api/fund/pnlreport";
    public const string GetFundIdFromOrderId = "/api/fundid/from/orderid";
    public const string GetFundWinLossRatio = "/api/fund/winlossratio";
    public const string GetFundDrawdownBalances = "/api/fund/drawdown/balances";
}

/// <summary>
/// Provides a collection of URI paths for querying system administrator endpoints.
/// </summary>
