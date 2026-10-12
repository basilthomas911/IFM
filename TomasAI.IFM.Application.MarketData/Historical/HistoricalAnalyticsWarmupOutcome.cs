using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>Identifies the result of one automatic coverage request.</summary>
public enum HistoricalAnalyticsWarmupOutcome
{
    Disabled,
    AlreadyCurrent,
    AcquiredAndReplayed,
    ReplayedFromStorage
}
