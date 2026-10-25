using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilitySeriesConstructionPolicy(
    [property: Key(0)] string ExpirySelectionPolicyVersion,
    [property: Key(1)] VolatilityInterpolationMethod ConstantTenorInterpolation,
    [property: Key(2)] string FuturesRollPolicyVersion,
    [property: Key(3)] string ExchangeCalendarVersion,
    [property: Key(4)] string ExchangeTimeZoneId,
    [property: Key(5)] string SamplingAndCutoffPolicyVersion,
    [property: Key(6)] TimeSpan IntradayCoalescingInterval,
    [property: Key(7)] int MaximumIntradayCheckpointsPerValueDate);
