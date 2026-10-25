using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Contains one immutable provider-neutral VX leg observation.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureLegObservation
{
    /// <summary>Gets the observed curve leg.</summary>
    [Key(0)] public FuturesVxTermStructureLeg Leg { get; init; }
    /// <summary>Gets the canonical domain contract ID.</summary>
    [Key(1)] public string ContractId { get; init; } = string.Empty;
    /// <summary>Gets the contract expiry.</summary>
    [Key(2)] public DateOnly Expiry { get; init; }
    /// <summary>Gets the positive trade price.</summary>
    [Key(3)] public decimal Price { get; init; }
    /// <summary>Gets the provider-neutral source sequence.</summary>
    [Key(4)] public long SourceSequence { get; init; }
    /// <summary>Gets the source event timestamp.</summary>
    [Key(5)] public DateTimeOffset SourceTimestampUtc { get; init; }
    /// <summary>Gets the market-data stream epoch.</summary>
    [Key(6)] public Guid StreamEpochId { get; init; }
}
