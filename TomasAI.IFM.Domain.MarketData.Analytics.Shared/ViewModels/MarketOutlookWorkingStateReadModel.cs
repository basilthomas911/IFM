using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>Identifies the lifecycle stage of a projected Market Outlook working state.</summary>
public enum MarketOutlookStateStatus
{
    /// <summary>The aggregate is collecting asynchronous market analytics.</summary>
    Collecting,

    /// <summary>The aggregate has published its current EOD snapshot.</summary>
    Published
}

/// <summary>Identifies a component stream accumulated by the Market Outlook aggregate.</summary>
public enum MarketOutlookComponentType
{
    /// <summary>Intraday RSI component.</summary>
    Rsi,
    /// <summary>Intraday TDI component.</summary>
    Tdi,
    /// <summary>ITI trend direction-change component.</summary>
    ItiDirection,
    /// <summary>ITI trend extreme-change component.</summary>
    ItiExtreme,
    /// <summary>ITI trend reversal-change component.</summary>
    ItiReversal,
    /// <summary>VX futures price component.</summary>
    Vix,
    /// <summary>EOD publication-boundary component.</summary>
    Eod
}

/// <summary>Captures the last accepted source position for one Market Outlook component stream.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSourceWatermark
{
    /// <summary>Gets the component stream.</summary>
    [Key(0)] public MarketOutlookComponentType ComponentType { get; init; }
    /// <summary>Gets the stable source event identity.</summary>
    [Key(1)] public Guid SourceEventId { get; init; }
    /// <summary>Gets the durable source sequence when one is available.</summary>
    [Key(2)] public long SourceEventSequence { get; init; }
    /// <summary>Gets the source event timestamp.</summary>
    [Key(3)] public DateTime SourceEventTimestamp { get; init; }
}

/// <summary>
/// Represents the immutable, replayable working state accumulated for one Market Outlook entity.
/// </summary>
/// <remarks>
/// PostgreSQL domain events are authoritative. This contract is carried by those events and will
/// also become the MOS-5 ScyllaDB working-state projection.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookWorkingStateReadModel
{
    /// <summary>Gets the aggregate identity.</summary>
    [Key(0)] public MarketOutlookEntityId EntityId { get; init; } = new();

    /// <summary>Gets the monotonically increasing working-state revision.</summary>
    [Key(1)] public long Revision { get; init; }

    /// <summary>Gets the time at which this state transition was created.</summary>
    [Key(2)] public DateTime UpdatedOn { get; init; }

    /// <summary>Gets the selected intraday RSI input.</summary>
    [Key(3)] public FuturesRsiSignalReadModel? FuturesRsiSignal { get; init; }

    /// <summary>Gets the selected intraday TDI input.</summary>
    [Key(4)] public FuturesTdiSignalReadModel? FuturesTdiSignal { get; init; }

    /// <summary>Gets the daily ITI direction-change input.</summary>
    [Key(5)] public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }

    /// <summary>Gets the daily ITI extreme-change input.</summary>
    [Key(6)] public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }

    /// <summary>Gets the daily ITI reversal-change input.</summary>
    [Key(7)] public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }

    /// <summary>Gets the selected VX futures price.</summary>
    [Key(8)] public decimal VixFuturesPrice { get; init; }

    /// <summary>Gets the EOD input used at the publication boundary.</summary>
    [Key(9)] public FuturesEodDataV2ReadModel? FuturesEodData { get; init; }

    /// <summary>Gets the latest published UI snapshot, when one exists.</summary>
    [Key(10)] public MarketOutlookSnapshotReadModel? PublishedSnapshot { get; init; }

    /// <summary>Gets the bounded last-accepted watermark for each component stream.</summary>
    [Key(11)] public MarketOutlookSourceWatermark[] SourceWatermarks { get; init; } = [];

    /// <summary>Gets the current lifecycle stage.</summary>
    [Key(12)] public MarketOutlookStateStatus Status { get; init; }

    /// <summary>Gets whether all analytics required to compute a trade signal are present.</summary>
    [IgnoreMember]
    public bool HasAllAnalytics => FuturesRsiSignal is not null
        && FuturesTdiSignal is not null
        && TrendDirectionChange is not null
        && TrendExtremeChange is not null
        && TrendReversalChange is not null
        && VixFuturesPrice > 0;

    /// <summary>Gets whether this checkpoint has a valid published snapshot.</summary>
    [IgnoreMember]
    public bool HasPublishedSnapshot => PublishedSnapshot is { IsValid: true };
}
