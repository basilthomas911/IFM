using System.Collections.Immutable;
using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

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

    public static VolatilityCalculationObservation From(OptionIvObservation observation) 
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new(
            observation.ObservationId,
            observation.ExchangeValueDate,
            observation.ImpliedVolatility,
            observation.Unit,
            observation.Status);
    }
}
