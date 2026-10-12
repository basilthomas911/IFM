using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialExecutionLeg([property: Key(0)] int TradeId, [property: Key(1)] string InstrumentId,
    [property: Key(2)] string RawSymbol, [property: Key(3)] string InstrumentClass, [property: Key(4)] string Side,
    [property: Key(5)] int Contracts, [property: Key(6)] decimal Multiplier);
