using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Identifies what caused the latest versionless Market Outlook cache replacement.</summary>
public enum MarketOutlookRefreshTrigger : byte
{
    None = 0,
    Component = 1,
    EsTrade = 2,
    EodSession = 3,
    Warmup = 4
}

/// <summary>Describes the current usability of one Market Outlook input.</summary>
public enum MarketOutlookInputAvailability : byte
{
    Unavailable = 0,
    Warming = 1,
    Available = 2,
    Stale = 3,
    Invalid = 4
}

/// <summary>
/// Current non-authoritative Market Outlook display value. The record is an immutable hot-cache
/// projection and deliberately has no aggregate revision or persistence lifecycle.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public DateTime UpdatedAtUtc { get; init; }
    [Key(3)] public DateTime MarketDataAsOfUtc { get; init; }
    [Key(4)] public MarketOutlookRefreshTrigger RefreshTrigger { get; init; }
    [Key(5)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();
    [Key(6)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(7)] public string MissingInputs { get; init; } = string.Empty;
    [Key(8)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }
    [Key(9)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }
    [Key(10)] public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }
    [Key(11)] public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }
    [Key(12)] public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }
    [Key(13)] public decimal? VixFuturesPrice { get; init; }
    [Key(14)] public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    [Key(15)] public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    [Key(16)] public FuturesItiSignalV2ReadModel? LatestItiTrendSignal { get; init; }
    [Key(17)] public MarketOutlookInputAvailability EsPriceAvailability { get; init; }
    [Key(18)] public MarketOutlookInputAvailability RsiAvailability { get; init; }
    [Key(19)] public MarketOutlookInputAvailability TdiAvailability { get; init; }
    [Key(20)] public MarketOutlookInputAvailability ItiAvailability { get; init; }
    [Key(21)] public MarketOutlookInputAvailability VxAvailability { get; init; }
    [Key(22)] public MarketOutlookInputAvailability DailyAnalyticsAvailability { get; init; }
    [Key(23)] public string FeedHealth { get; init; } = "Unknown";

    [IgnoreMember]
    public bool IsComplete => FuturesEodData.IsValid
        && FuturesRsiSignal is { IsWarm: true, RSI: >= 0d }
        && LatestItiTrendSignal is not null
        && VixFuturesPrice > 0;

    [IgnoreMember]
    public bool HasWarmDailyAnalytics => FuturesEmaSignal is { IsWarm: true }
        && FuturesBbSignal is { IsWarm: true };

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && (EsPriceAvailability == MarketOutlookInputAvailability.Available
            || FuturesEodData.IsValid
            || FuturesTradeSignal is not null
            || FuturesRsiSignal is not null
            || FuturesTdiSignal is not null
            || LatestItiTrendSignal is not null
            || VixFuturesPrice > 0
            || FuturesEmaSignal is not null
            || FuturesBbSignal is not null);
}
