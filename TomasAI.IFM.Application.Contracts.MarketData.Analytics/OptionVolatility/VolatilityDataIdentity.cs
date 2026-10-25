using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityDataIdentity(
    [property: Key(0)] string Environment,
    [property: Key(1)] string Provider,
    [property: Key(2)] string Dataset);
