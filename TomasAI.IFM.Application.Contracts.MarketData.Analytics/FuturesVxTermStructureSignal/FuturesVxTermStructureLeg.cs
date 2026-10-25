using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

/// <summary>Identifies which VX curve leg supplied a price observation.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesVxTermStructureLeg : byte
{
    /// <summary>No leg was identified.</summary>
    Unknown = 0,
    /// <summary>The first eligible VX expiry.</summary>
    Front = 1,
    /// <summary>The immediately following VX expiry.</summary>
    Back = 2
}
