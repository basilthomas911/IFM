using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record CompositionFutureDefinition(
    [property: Key(0)] string ContractId, [property: Key(1)] string Root,
    [property: Key(2)] string Dataset, [property: Key(3)] string Exchange,
    [property: Key(4)] string Currency, [property: Key(5)] DateTimeOffset LastTradingUtc,
    [property: Key(6)] decimal Multiplier, [property: Key(7)] decimal TickSize,
    [property: Key(8)] string DefinitionDigest);
