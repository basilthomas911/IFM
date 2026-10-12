using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Compact normalized price change consumed by the realtime route index.</summary>
public readonly record struct PositionMarketTick(
    string ContractId,
    decimal Price,
    long SourceSequence,
    DateTime OccurredAtUtc);
