using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Contains feed-neutral conditions relevant to exact VWAP eligibility.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "Preserve the existing public collection type name.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2344", Justification = "The enum-shaped compatibility type retains its existing public identity.")]
[Flags]
public enum FuturesVwapTradeConditionFlags : ushort
{
    /// <summary>No condition was reported.</summary>
    None = 0,
    /// <summary>The source marked the trade as a snapshot value.</summary>
    Snapshot = 1 << 0,
    /// <summary>The source marked the price as undefined.</summary>
    UndefinedPrice = 1 << 1,
    /// <summary>The source marked the observation as historical replay.</summary>
    Replay = 1 << 2
}
