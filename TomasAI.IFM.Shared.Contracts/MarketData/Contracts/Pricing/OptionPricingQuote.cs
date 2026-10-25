using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

/// <summary>Immutable source-time quote. A generation change fences all preceding readiness.</summary>
/// <param name="ContractId">The futures or option contract identifier.</param>
/// <param name="Bid">The bid.</param>
/// <param name="Ask">The ask.</param>
/// <param name="BidSize">The quantity available at the best bid.</param>
/// <param name="AskSize">The quantity available at the best ask.</param>
/// <param name="EventAtUtc">The event at utc.</param>
/// <param name="ReceivedAtUtc">The received at utc.</param>
/// <param name="Sequence">The sequence.</param>
/// <param name="GenerationId">The generation id.</param>
[MessagePackObject]
public sealed record OptionPricingQuote(
    [property: Key(0)] string ContractId,
    [property: Key(1)] decimal Bid,
    [property: Key(2)] decimal Ask,
    [property: Key(3)] decimal BidSize,
    [property: Key(4)] decimal AskSize,
    [property: Key(5)] DateTimeOffset EventAtUtc,
    [property: Key(6)] DateTimeOffset ReceivedAtUtc,
    [property: Key(7)] long Sequence,
    [property: Key(8)] Guid GenerationId);
