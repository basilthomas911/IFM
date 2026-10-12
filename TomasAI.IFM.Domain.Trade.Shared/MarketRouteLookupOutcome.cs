using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketRouteLookupOutcome : byte
{
    Routed,
    NoOpenPosition,
    StaleRoute,
    UnknownInstrument,
    InvalidTick,
    DuplicateOrOutOfOrder
}
