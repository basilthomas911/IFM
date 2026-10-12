using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Identifies how the persisted VWAP was calculated.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesVwapCalculationMethod : byte
{
    /// <summary>No calculation method is available.</summary>
    Unknown = 0,
    /// <summary>Every eligible executed trade contributed its exact price and size.</summary>
    TickExact = 1
}
