namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Identifies the market lifecycle projects in the scheduler host catalog.</summary>
public static class ScheduledTaskKeys
{
    /// <summary>Stops feeds and finalizes the ended futures session.</summary>
    public const string FuturesMarketClose = "futures-market-close";
    /// <summary>Identifies API-owned close receipts separately from scheduler process receipts.</summary>
    public const string ApiMarketCloseHost = "api-market-close";
    /// <summary>Identifies the terminal close stage after transport maintenance.</summary>
    public const string MarketClosedStage = "MarketClosed";
    /// <summary>Starts the next finalized futures session.</summary>
    public const string FuturesMarketOpen = "futures-market-open";
}
