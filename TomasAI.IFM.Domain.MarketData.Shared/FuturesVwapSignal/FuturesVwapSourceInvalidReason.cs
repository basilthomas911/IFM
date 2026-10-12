using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the byte-backed persisted/message enum representation.")]
public enum FuturesVwapSourceInvalidReason : byte
{
    None = 0,
    DeliveryGap = 1,
    StreamEpochChanged = 2,
    UncorrelatableCorrection = 3,
    InvalidTrade = 4,
    RecoveryIncomplete = 5
}
