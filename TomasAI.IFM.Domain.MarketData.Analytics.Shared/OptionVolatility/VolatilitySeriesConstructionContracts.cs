using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public enum VolatilitySeriesConstructionFailure
{
    None = 0,
    NoQualifiedInputs = 1,
    MissingTenorBracket = 2,
    MissingOptionSide = 3,
    ExactUnderlyingMismatch = 4,
    IncompatibleConvention = 5,
    IncompatibleSource = 6,
    StaleInput = 7,
    InvalidImpliedVolatility = 8,
    InvalidDefinition = 9
}

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

public sealed record VolatilitySeriesConstructionRequest(
    VolatilitySeriesDefinition Definition,
    DateOnly ExchangeValueDate,
    string SamplingSlot,
    DateTimeOffset AsOfUtc,
    int Revision,
    string? SupersedesObservationId,
    ImmutableArray<QualifiedOptionIvInput> Inputs);

public sealed record VolatilitySeriesConstructionResult(
    OptionIvObservation Observation,
    VolatilitySeriesConstructionFailure Failure,
    ImmutableArray<string> DiagnosticCodes);

public enum VolatilitySampleKind
{
    DailyFinal = 1,
    IntradayCheckpoint = 2
}

public sealed record VolatilitySampleDecision(
    bool Accepted,
    bool ReplacedCoalescedCheckpoint,
    string ReasonCode);
