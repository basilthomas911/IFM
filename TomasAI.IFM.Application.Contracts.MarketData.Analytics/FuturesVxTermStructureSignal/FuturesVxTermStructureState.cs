using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Classifies the shape of the first two eligible VX futures contracts.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesVxTermStructureState : byte
{
    /// <summary>The curve cannot yet be classified.</summary>
    Unknown = 0,
    /// <summary>The back contract trades above the front contract.</summary>
    Contango = 1,
    /// <summary>The two contracts trade within the configured flat tolerance.</summary>
    Flat = 2,
    /// <summary>The front contract trades above the back contract.</summary>
    Backwardation = 3
}
