using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

[MessagePackObject]
public sealed record VolatilityContributor(
    [property: Key(0)] string OptionContractId,
    [property: Key(1)] string UnderlyingFuturesContractId,
    [property: Key(2)] DateTimeOffset QuoteObservedAtUtc,
    [property: Key(3)] DateTimeOffset UnderlyingObservedAtUtc,
    [property: Key(4)] long QuoteSequence,
    [property: Key(5)] long UnderlyingSequence,
    [property: Key(6)] Guid SourceGenerationId);
