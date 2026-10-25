using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilitySeriesIdentity(
    [property: Key(0)] string SeriesId,
    [property: Key(1)] string MethodologyVersion);
