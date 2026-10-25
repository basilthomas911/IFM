using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Contains replayable paired-leg state owned by the Command actor.</summary>
[MessagePackObject]
public sealed record FuturesVxTermStructureCheckpoint
{
    /// <summary>Gets the latest accepted front observation.</summary>
    [Key(0)] public FuturesVxTermStructureLegObservation? Front { get; init; }
    /// <summary>Gets the latest accepted back observation.</summary>
    [Key(1)] public FuturesVxTermStructureLegObservation? Back { get; init; }
    /// <summary>Gets the most recently emitted front/back ratio.</summary>
    [Key(2)] public decimal? PreviousFrontBackRatio { get; init; }
    /// <summary>Gets the most recently emitted term-structure percentage.</summary>
    [Key(3)] public decimal? PreviousTermStructurePercent { get; init; }
}
