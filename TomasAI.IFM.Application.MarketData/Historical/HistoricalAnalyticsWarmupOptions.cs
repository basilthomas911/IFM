using TomasAI.IFM.Application.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Historical;

/// <summary>Controls automatic Development-only historical Analytics warm-up.</summary>
public sealed record HistoricalAnalyticsWarmupOptions
{
    public bool Enabled { get; init; }
    public bool IsDevelopmentEnvironment { get; init; }
    public int LookbackCalendarDays { get; init; } = 365;
    public int MinimumValidDailySessions { get; init; } = 201;
    public int TrailingProviderAvailabilityGraceSessions { get; init; } = 1;
    public decimal MaximumCostUsd { get; init; } = 10m;
    public long MaximumBytes { get; init; } = 1_073_741_824;
    public string NormalizationVersion { get; init; } = "historical-daily-v1";
    public string CalculationConfigurationVersion { get; init; } = "ema-bb-daily-v1";

    /// <summary>Gets whether automatic provider acquisition and replay are permitted.</summary>
    public bool AutomaticLoadingPermitted => Enabled && IsDevelopmentEnvironment;

    public HistoricalAnalyticsWarmupOptions Validate()
    {
        if (LookbackCalendarDays < 365)
            throw new ArgumentOutOfRangeException(nameof(LookbackCalendarDays));
        if (MinimumValidDailySessions < 201)
            throw new ArgumentOutOfRangeException(nameof(MinimumValidDailySessions));
        if (TrailingProviderAvailabilityGraceSessions is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(TrailingProviderAvailabilityGraceSessions));
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
    IgnoredInProduction,
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
