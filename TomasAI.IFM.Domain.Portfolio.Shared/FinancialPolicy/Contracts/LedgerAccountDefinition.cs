using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record LedgerAccountDefinition([property: Key(0)] int AccountId, [property: Key(1)] long Version,
    [property: Key(2)] string Category, [property: Key(3)] PostingSide NormalSide,
    [property: Key(4)] bool FundDimensionRequired, [property: Key(5)] string ContentHash);
