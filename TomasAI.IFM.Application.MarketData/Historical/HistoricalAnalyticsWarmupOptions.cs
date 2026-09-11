using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>Controls automatic historical Analytics initialization.</summary>
public sealed record HistoricalAnalyticsWarmupOptions
{
    public bool Enabled { get; init; }
    public int LookbackCalendarDays { get; init; } = 365;
    public int MinimumValidDailySessions { get; init; } = 200;
    public decimal MaximumCostUsd { get; init; } = 10m;
    public long MaximumBytes { get; init; } = 1_073_741_824;
    public string NormalizationVersion { get; init; } = "historical-daily-v1";
    public string CalculationConfigurationVersion { get; init; } = "ema-bb-daily-v1";

    /// <summary>Gets whether automatic provider acquisition and replay are permitted.</summary>
    public bool AutomaticLoadingPermitted => Enabled;

    public HistoricalAnalyticsWarmupOptions Validate()
    {
        if (LookbackCalendarDays < 365)
            throw new ArgumentOutOfRangeException(nameof(LookbackCalendarDays));
        if (MinimumValidDailySessions < 200)
            throw new ArgumentOutOfRangeException(nameof(MinimumValidDailySessions));
        if (MaximumCostUsd <= 0 || MaximumBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumCostUsd));
        ArgumentException.ThrowIfNullOrWhiteSpace(NormalizationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(CalculationConfigurationVersion);
        return this;
    }
}

/// <summary>Identifies the result of one automatic coverage request.</summary>
public enum HistoricalAnalyticsWarmupOutcome
{
    Disabled,
    AlreadyCurrent,
    AcquiredAndReplayed,
    ReplayedFromStorage
}

/// <summary>Reports bounded coverage and replay results without provider records.</summary>
public sealed record HistoricalAnalyticsWarmupResult(
    HistoricalAnalyticsWarmupOutcome Outcome,
    DateOnly StartDate,
    DateOnly EndDate,
    int ValidSessionCount,
    int MissingSessionCount,
    HistoricalDataLoaderState? LastLoadState);
