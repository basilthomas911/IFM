using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

/// <summary>Immutable source-time quote. A generation change fences all preceding readiness.</summary>
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
