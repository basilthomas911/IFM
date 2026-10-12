using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Describes the lifecycle action of a VWAP trade observation.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesVwapTradeAction : byte
{
    /// <summary>The source action was absent or unsupported.</summary>
    Unknown = 0,
    /// <summary>A new executed trade was reported.</summary>
    New = 1,
    /// <summary>An existing trade was changed.</summary>
    Change = 2,
    /// <summary>An existing trade was cancelled.</summary>
    Cancel = 3,
    /// <summary>An existing trade was corrected.</summary>
    Correct = 4,
    /// <summary>The source cleared its trade state.</summary>
    Clear = 5,
    /// <summary>The source supplied no action.</summary>
    None = 6
}
