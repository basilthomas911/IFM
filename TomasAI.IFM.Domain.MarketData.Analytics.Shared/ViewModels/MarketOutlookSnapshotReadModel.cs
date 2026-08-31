using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// One coherent Market Outlook display snapshot. Each independently accepted component advances
/// the snapshot; EOD is one component and is not a gate for the other analytics.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public DateTime UpdatedOn { get; init; }
    [Key(4)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();
    [Key(5)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(6)] public string MissingInputs { get; init; } = string.Empty;
    [Key(7)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }
    [Key(8)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }
    [Key(9)] public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }
    [Key(10)] public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }
    [Key(11)] public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }
    [Key(12)] public decimal? VixFuturesPrice { get; init; }
    [Key(13)] public FuturesEmaSignalReadModel? FuturesEmaSignal { get; init; }
    [Key(14)] public FuturesBbSignalReadModel? FuturesBbSignal { get; init; }
    [Key(15)] public FuturesItiSignalV2ReadModel? LatestItiTrendSignal { get; init; }

    [IgnoreMember]
    public bool IsComplete => FuturesEodData.IsValid
        && FuturesRsiSignal is { IsWarm: true, RSI: >= 0d }
        && LatestItiTrendSignal is not null
        && VixFuturesPrice > 0;

    /// <summary>Gets whether both Daily indicator families are fully warm.</summary>
    [IgnoreMember]
    public bool HasWarmDailyAnalytics => FuturesEmaSignal is { IsWarm: true }
        && FuturesBbSignal is { IsWarm: true };

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && Revision > 0
        && (FuturesEodData.IsValid
            || FuturesRsiSignal is not null
            || FuturesTdiSignal is not null
            || TrendDirectionChange is not null
            || TrendExtremeChange is not null
            || TrendReversalChange is not null
            || LatestItiTrendSignal is not null
            || VixFuturesPrice > 0
            || FuturesEmaSignal is not null
            || FuturesBbSignal is not null);

    public MarketOutlookSnapshotReadModel() { }

    [SerializationConstructor]
    public MarketOutlookSnapshotReadModel(
        string contractId,
        DateOnly valueDate,
        long revision,
        DateTime updatedOn,
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal,
        string missingInputs,
        FuturesRsiSignalReadModel? futuresRsiSignal = null,
        FuturesTdiSignalReadModel? futuresTdiSignal = null,
        FuturesItiSignalV2ReadModel? trendDirectionChange = null,
        FuturesItiSignalV2ReadModel? trendExtremeChange = null,
        FuturesItiSignalV2ReadModel? trendReversalChange = null,
        decimal? vixFuturesPrice = null,
        FuturesEmaSignalReadModel? futuresEmaSignal = null,
        FuturesBbSignalReadModel? futuresBbSignal = null,
        FuturesItiSignalV2ReadModel? latestItiTrendSignal = null)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Revision = revision;
        UpdatedOn = updatedOn;
        FuturesEodData = futuresEodData ?? new FuturesEodDataV2ReadModel();
        FuturesTradeSignal = futuresTradeSignal;
        MissingInputs = missingInputs ?? string.Empty;
        FuturesRsiSignal = futuresRsiSignal;
        FuturesTdiSignal = futuresTdiSignal;
        TrendDirectionChange = trendDirectionChange;
        TrendExtremeChange = trendExtremeChange;
        TrendReversalChange = trendReversalChange;
        VixFuturesPrice = vixFuturesPrice;
        FuturesEmaSignal = futuresEmaSignal;
        FuturesBbSignal = futuresBbSignal;
        LatestItiTrendSignal = latestItiTrendSignal;
    }
}
