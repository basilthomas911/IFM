using MessagePack;
using System.Collections.Immutable;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing;
[MessagePackObject]
public sealed record CompositionFutureDefinition(
    [property: Key(0)] string ContractId, [property: Key(1)] string Root,
    [property: Key(2)] string Dataset, [property: Key(3)] string Exchange,
    [property: Key(4)] string Currency, [property: Key(5)] DateTimeOffset LastTradingUtc,
    [property: Key(6)] decimal Multiplier, [property: Key(7)] decimal TickSize,
    [property: Key(8)] string DefinitionDigest);

[MessagePackObject]
public sealed record CompositionMarketInstrument(
    [property: Key(0)] string ContractId,
    [property: Key(1)] OptionPricingQuote Quote,
    [property: Key(2)] OptionPricingContext? Pricing,
    [property: Key(3)] decimal? Strike,
    [property: Key(4)] bool? IsCall,
    [property: Key(5)] OptionPricingQuote? Underlying,
    [property: Key(6)] CompositionFutureDefinition? FutureDefinition = null);

[MessagePackObject]
public sealed record CompositionInstrumentSnapshot(
    [property: Key(0)] CompositionMarketInstrument Instrument,
    [property: Key(1)] OptionPricingValue? Valuation);

[MessagePackObject]
public sealed record MarketCompositionSnapshot(
    [property: Key(0)] int SchemaVersion,
    [property: Key(1)] Guid SnapshotId,
    [property: Key(2)] string ScopeId,
    [property: Key(3)] string ScopeToken,
    [property: Key(4)] string Horizon,
    [property: Key(5)] Guid GenerationId,
    [property: Key(6)] DateTimeOffset EvaluatedAtUtc,
    [property: Key(7)] DateTimeOffset ValidUntilUtc,
    [property: Key(8)] ImmutableArray<CompositionInstrumentSnapshot> Instruments,
    [property: Key(9)] string Digest);

[MessagePackObject]
public sealed record OptionPricingValue(
    [property: Key(0)] double ImpliedVolatility,
    [property: Key(1)] double Delta,
    [property: Key(2)] double Gamma,
    [property: Key(3)] double Theta,
    [property: Key(4)] double Vega,
    [property: Key(5)] double Rho,
    [property: Key(6)] double TheoreticalPrice,
    [property: Key(7)] double TimeToExpiry,
    [property: Key(8)] string ContextDigest);
