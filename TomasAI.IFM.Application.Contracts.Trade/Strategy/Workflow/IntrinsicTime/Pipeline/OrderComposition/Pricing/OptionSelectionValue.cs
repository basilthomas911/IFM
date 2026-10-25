using MessagePack;
using System.Collections.Immutable;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;

[MessagePackObject]
public sealed record OptionSelectionValue(
    [property: Key(0)] double Price, [property: Key(1)] double Delta, [property: Key(2)] double ImpliedVolatility,
    [property: Key(3)] OptionPricingQuote IvUnderlying, [property: Key(4)] OptionPricingQuote IvOption,
    [property: Key(5)] DateTimeOffset IvCalculatedAtUtc, [property: Key(6)] DateTimeOffset CalculatedAtUtc,
    [property: Key(7)] DateTimeOffset ValidUntilUtc, [property: Key(8)] string PolicyVersion,
    [property: Key(9)] string ContextDigest);
