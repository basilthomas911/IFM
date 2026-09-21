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

[MessagePackObject]
public sealed record VolatilityObservationProvenance(
    [property: Key(0)] ImmutableArray<VolatilityContributor> Contributors,
    [property: Key(1)] string PricerVersion,
    [property: Key(2)] string InputDigest,
    [property: Key(3)] string SourceMethodologyEvidenceId);

/// <summary>One immutable, comparable IV observation for a configured value-date sampling slot.</summary>
[MessagePackObject]
public sealed record OptionIvObservation(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] string ObservationId,
    [property: Key(2)] VolatilitySeriesIdentity Series,
    [property: Key(3)] DateOnly ExchangeValueDate,
    [property: Key(4)] string SamplingSlot,
    [property: Key(5)] decimal? ImpliedVolatility,
    [property: Key(6)] VolatilityValueUnit Unit,
    [property: Key(7)] VolatilityObservationStatus Status,
    [property: Key(8)] string StatusReason,
    [property: Key(9)] DateTimeOffset ObservedAtUtc,
    [property: Key(10)] DateTimeOffset RecordedAtUtc,
    [property: Key(11)] DateTimeOffset AvailableAtUtc,
    [property: Key(12)] int Revision,
    [property: Key(13)] string? SupersedesObservationId,
    [property: Key(14)] VolatilityObservationProvenance Provenance)
{
    public const ushort CurrentSchemaVersion = 1;
}

/// <summary>Minimal immutable input used by the pure rolling calculation.</summary>
public sealed record VolatilityCalculationObservation(
    string ObservationId,
    DateOnly ExchangeValueDate,
    decimal? ImpliedVolatility,
    VolatilityValueUnit Unit,
    VolatilityObservationStatus Status)
{
    public static VolatilityCalculationObservation FromFloatingPoint(
        string observationId,
        DateOnly exchangeValueDate,
        double impliedVolatility,
        VolatilityObservationStatus status = VolatilityObservationStatus.Qualified)
    {
        if (!double.IsFinite(impliedVolatility) ||
            impliedVolatility < (double)decimal.MinValue ||
            impliedVolatility > (double)decimal.MaxValue)
        {
            return new(
                observationId,
                exchangeValueDate,
                null,
                VolatilityValueUnit.AnnualDecimal,
                VolatilityObservationStatus.Invalid);
        }

        return new(
            observationId,
            exchangeValueDate,
            (decimal)impliedVolatility,
            VolatilityValueUnit.AnnualDecimal,
            status);
    }

    public static VolatilityCalculationObservation From(OptionIvObservation observation) =>
        new(
            observation.ObservationId,
            observation.ExchangeValueDate,
            observation.ImpliedVolatility,
            observation.Unit,
            observation.Status);
}

/// <summary>The exact expected prior exchange sessions. Dates outside this window are never substitutes.</summary>
public sealed record VolatilityCalculationWindow(
    DateOnly CurrentExchangeValueDate,
    ImmutableArray<DateOnly> PriorExchangeSessionDates);
