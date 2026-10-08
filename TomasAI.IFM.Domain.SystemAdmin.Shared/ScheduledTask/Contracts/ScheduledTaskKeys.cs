namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Identifies the market lifecycle projects in the scheduler host catalog.</summary>
public static class ScheduledTaskKeys
{
    /// <summary>Stops feeds and finalizes the ended futures session.</summary>
    public const string FuturesMarketClose = "futures-market-close";
    /// <summary>Starts the next finalized futures session.</summary>
    public const string FuturesMarketOpen = "futures-market-open";
}
