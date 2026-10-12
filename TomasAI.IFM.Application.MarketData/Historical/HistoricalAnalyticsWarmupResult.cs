using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>Reports bounded coverage and replay results without provider records.</summary>
public sealed record HistoricalAnalyticsWarmupResult(
    HistoricalAnalyticsWarmupOutcome Outcome,
    DateOnly StartDate,
    DateOnly EndDate,
    int ValidSessionCount,
    int MissingSessionCount,
    HistoricalDataLoaderState? LastLoadState);
