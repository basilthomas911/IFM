using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>A qualified IV-only result. It deliberately contains no Greeks.</summary>
public sealed record QualifiedOptionIvInput(
    string OptionContractId,
    string UnderlyingFuturesContractId,
    string UnderlyingRoot,
    string Environment,
    string ProductFamily,
    string Venue,
    string Currency,
    string Provider,
    string Dataset,
    string ExerciseConvention,
    string PremiumConvention,
    string SettlementConvention,
    string PricerVersion,
    string NumericalPolicyVersion,
    DateTimeOffset ExpirationUtc,
    decimal Strike,
    bool IsCall,
    decimal ImpliedVolatility,
    decimal UnderlyingPrice,
    DateTimeOffset QuoteObservedAtUtc,
    DateTimeOffset UnderlyingObservedAtUtc,
    long QuoteSequence,
    long UnderlyingSequence,
    Guid SourceGenerationId,
    string InputDigest);
