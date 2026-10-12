namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Provider-neutral route-health classifications for one owned feed route.</summary>
public enum MarketDataFeedSessionHealthState
{
    Inactive = 0,
    OffHoursActive = 1,
    OffHoursDegraded = 2,
    Green = 3,
    Yellow = 4,
    Red = 5
}
