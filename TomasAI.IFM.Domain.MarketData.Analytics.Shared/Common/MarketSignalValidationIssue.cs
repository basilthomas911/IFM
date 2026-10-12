using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Provides stable reason codes for invalid or incomplete market signal inputs.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketSignalValidationIssue : byte
{
    /// <summary>No validation issue was detected.</summary>
    None = 0,

    /// <summary>The source observation is incomplete.</summary>
    IncompleteObservation = 1,

    /// <summary>The OHLC price relationship is invalid.</summary>
    InvalidOhlc = 2,

    /// <summary>Required volume is missing.</summary>
    MissingVolume = 3,

    /// <summary>Required trade lineage is missing.</summary>
    MissingTradeLineage = 4,

    /// <summary>A gap exists in source delivery lineage.</summary>
    SourceGap = 5,

    /// <summary>The market series does not match the requested calculation.</summary>
    SeriesMismatch = 6,

    /// <summary>The observation does not match a composed calculation input.</summary>
    ObservationMismatch = 7,

    /// <summary>The source is too stale for the calculation.</summary>
    StaleSource = 8,

    /// <summary>The calculation produced an invalid result.</summary>
    InvalidCalculation = 9
}
