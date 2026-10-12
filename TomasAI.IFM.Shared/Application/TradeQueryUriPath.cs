namespace TomasAI.IFM.Shared.Application;

/// <summary>
/// Provides a collection of URI paths for querying trade endpoints.
/// </summary>
/// <remarks>This class contains constant string fields representing the URI paths for various endpoints in the
/// trade API. These endpoints are used to retrieve trade history, option trades, trade limits, positions, and related data.
/// The URIs are intended to be used as part of HTTP requests to the API.</remarks>
public static class TradeQueryUriPath
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
