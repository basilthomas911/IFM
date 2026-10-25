using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Specifies which provider-neutral market-series identity is populated.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketSeriesIdentityKind : byte
{
    /// <summary>No market series is identified.</summary>
    Unknown = 0,

    /// <summary>The identity refers to one specific futures contract.</summary>
    Contract = 1,

    /// <summary>The identity refers to a roll-aware futures continuation.</summary>
    FuturesContinuation = 2
}
