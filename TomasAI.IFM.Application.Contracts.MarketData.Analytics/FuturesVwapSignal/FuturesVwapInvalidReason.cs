using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;

/// <summary>Identifies why an exact futures-session VWAP is unavailable.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum FuturesVwapInvalidReason : byte
{
    /// <summary>No invalid condition exists.</summary>
    None = 0,
    /// <summary>One or more delivered trade ordinals were missing.</summary>
    DeliveryGap = 1,
    /// <summary>The market-data stream epoch changed during the session.</summary>
    StreamEpochChanged = 2,
    /// <summary>A trade correction could not be correlated deterministically.</summary>
    UncorrelatableCorrection = 3,
    /// <summary>The trade input was structurally invalid.</summary>
    InvalidTrade = 4,
    /// <summary>An exact historical recovery is still incomplete.</summary>
    RecoveryIncomplete = 5
}
