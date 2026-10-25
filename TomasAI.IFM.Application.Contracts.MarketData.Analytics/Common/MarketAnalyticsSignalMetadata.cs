using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>
/// Carries common identity, provenance, configuration, and validity metadata for an analytics signal.
/// </summary>
[MessagePackObject]
public sealed record MarketAnalyticsSignalMetadata
{
    /// <summary>Gets the exact configured signal key.</summary>
    [Key(0)] public MarketAnalyticsSignalKey SignalKey { get; init; }

    /// <summary>Gets the actual source contract used by this calculation.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;

    /// <summary>Gets the futures trading value date.</summary>
    [Key(2)] public DateOnly ValueDate { get; init; }

    /// <summary>Gets the source observation identity.</summary>
    [Key(3)] public FuturesTradeSessionBarId ObservationId { get; init; }

    /// <summary>Gets the last exchange event included in the calculation.</summary>
    [Key(4)] public DateTimeOffset MarketDataAsOfUtc { get; init; }

    /// <summary>Gets the UTC time at which calculation completed.</summary>
    [Key(5)] public DateTimeOffset CalculatedAtUtc { get; init; }

    /// <summary>Gets the last accepted source sequence included in the calculation.</summary>
    [Key(6)] public long SourceSequence { get; init; }

    /// <summary>Gets the serialized signal schema version.</summary>
    [Key(7)] public ushort SchemaVersion { get; init; }

    /// <summary>Gets the calculation implementation version.</summary>
    [Key(8)] public string CalculationVersion { get; init; } = string.Empty;

    /// <summary>Gets the method used to calculate the signal.</summary>
    [Key(9)] public MarketSignalCalculationMethod CalculationMethod { get; init; }

    /// <summary>Gets whether the persisted signal value passed formula validation.</summary>
    [Key(10)] public bool IsValid { get; init; }

    /// <summary>Gets stable issues explaining an invalid signal.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(11)] public MarketSignalValidationIssue[] ValidationIssues { get; init; } = [];

    /// <summary>Gets the Databento stream epoch paired with <see cref="SourceSequence"/>, when applicable.</summary>
    [Key(12)] public Guid StreamEpochId { get; init; }

    /// <summary>Gets the exact provider-neutral market series from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public MarketSeriesIdentity MarketSeriesIdentity => SignalKey.MarketSeriesIdentity;

    /// <summary>Gets the roll-aware continuation identity when this is a continuation signal.</summary>
    [IgnoreMember]
    public FuturesSeriesId? FuturesSeriesId => SignalKey.MarketSeriesIdentity.FuturesSeriesId;

    /// <summary>Gets the signal timeframe from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public TimeFrameType TimeFrame => SignalKey.TimeFrame;

    /// <summary>Gets the immutable calculation-configuration identity from <see cref="SignalKey"/>.</summary>
    [IgnoreMember]
    public string CalculationConfigurationId => SignalKey.CalculationConfigurationId;
}
