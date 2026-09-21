using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

/// <summary>Pure deterministic IV Rank/Percentile calculation over an explicit prior-session window.</summary>
public static class VolatilityRankPercentileCalculator
{
    public static VolatilityMetricCalculation Calculate(
        VolatilityMetricPolicy policy,
        VolatilityCalculationWindow window,
        VolatilityCalculationObservation current,
        IEnumerable<VolatilityCalculationObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(observations);
        ValidatePolicyAndWindow(policy, window, current);

        var expectedDates = window.PriorExchangeSessionDates.ToHashSet();
        var candidates = observations
            .Where(observation => expectedDates.Contains(observation.ExchangeValueDate))
            .OrderBy(observation => observation.ExchangeValueDate)
            .ThenBy(observation => observation.ObservationId, StringComparer.Ordinal)
            .ToArray();

        // More than one candidate for a configured daily slot is not silently resolved.
        var validHistory = candidates
            .GroupBy(observation => observation.ExchangeValueDate)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .Where(IsQualifiedValue)
            .ToArray();

        var expectedCount = window.PriorExchangeSessionDates.Length;
        var validCount = validHistory.Length;
        var coverage = expectedCount == 0 ? 0m : (decimal)validCount / expectedCount;
        var hasSufficientHistory = validCount >= policy.MinimumValidObservations &&
                                   coverage >= policy.MinimumCoverageRatio;
        var currentIsValid = current.ExchangeValueDate == window.CurrentExchangeValueDate &&
                             IsQualifiedValue(current);

        var sourceIds = candidates
            .Select(observation => observation.ObservationId)
            .Append(current.ObservationId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        var windowStart = window.PriorExchangeSessionDates.Min();
        var windowEnd = window.PriorExchangeSessionDates.Max();

        if (!currentIsValid)
        {
            return Result(
                current.ImpliedVolatility,
                null,
                VolatilityMetricStatus.InvalidCurrentObservation,
                null,
                VolatilityMetricStatus.InvalidCurrentObservation,
                null,
                null,
                0,
                0);
        }

        var currentValue = current.ImpliedVolatility!.Value;
        var belowCount = validHistory.Count(observation => observation.ImpliedVolatility!.Value < currentValue);
        var tieCount = validHistory.Count(observation => observation.ImpliedVolatility!.Value == currentValue);

        if (!hasSufficientHistory)
        {
            return Result(
                currentValue,
                null,
                VolatilityMetricStatus.InsufficientHistory,
                null,
                VolatilityMetricStatus.InsufficientHistory,
                null,
                null,
                belowCount,
                tieCount);
        }

        var rankLow = Math.Min(currentValue, validHistory.Min(x => x.ImpliedVolatility!.Value));
        var rankHigh = Math.Max(currentValue, validHistory.Max(x => x.ImpliedVolatility!.Value));
        var percentile = 100m * belowCount / validCount;
        if (rankHigh == rankLow)
        {
            return Result(
                currentValue,
                null,
                VolatilityMetricStatus.UndefinedRange,
                percentile,
                VolatilityMetricStatus.Qualified,
                rankLow,
                rankHigh,
                belowCount,
                tieCount);
        }

        var rank = 100m * (currentValue - rankLow) / (rankHigh - rankLow);
        return Result(
            currentValue,
            rank,
            VolatilityMetricStatus.Qualified,
            percentile,
            VolatilityMetricStatus.Qualified,
            rankLow,
            rankHigh,
            belowCount,
            tieCount);

        VolatilityMetricCalculation Result(
            decimal? currentIv,
            decimal? rank,
            VolatilityMetricStatus rankStatus,
            decimal? percentile,
            VolatilityMetricStatus percentileStatus,
            decimal? low,
            decimal? high,
            int below,
            int ties) =>
            new(
                currentIv,
                VolatilityValueUnit.AnnualDecimal,
                rank,
                rankStatus,
                percentile,
                percentileStatus,
                VolatilityMetricUnit.PercentagePoints0To100,
                low,
                high,
                below,
                ties,
                validCount,
                expectedCount,
                coverage,
                windowStart,
                windowEnd,
                sourceIds);
    }

    static bool IsQualifiedValue(VolatilityCalculationObservation observation) =>
        observation.Status == VolatilityObservationStatus.Qualified &&
        observation.Unit == VolatilityValueUnit.AnnualDecimal &&
        observation.ImpliedVolatility is >= 0m;

    static void ValidatePolicyAndWindow(
        VolatilityMetricPolicy policy,
        VolatilityCalculationWindow window,
        VolatilityCalculationObservation current)
    {
        if (policy.HistoricalLookbackSessions <= 0)
            throw new ArgumentOutOfRangeException(nameof(policy), "Lookback sessions must be positive.");
        if (policy.MinimumValidObservations <= 0 ||
            policy.MinimumValidObservations > policy.HistoricalLookbackSessions)
            throw new ArgumentOutOfRangeException(nameof(policy), "Minimum valid observations must be within the lookback.");
        if (policy.MinimumCoverageRatio is <= 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(policy), "Minimum coverage must be in (0, 1].");
        if (policy.GapPolicy != VolatilityGapPolicy.PreserveExpectedSessionGap ||
            policy.WindowConvention != VolatilityHistoricalWindowConvention.PriorExchangeSessions ||
            policy.RankRangeConvention != VolatilityRankRangeConvention.CurrentAndPriorWindow ||
            policy.PercentileTieConvention != VolatilityPercentileTieConvention.StrictlyLessThanCurrent)
            throw new ArgumentException("The policy uses an unsupported Stage 4 calculation convention.", nameof(policy));
        if (window.PriorExchangeSessionDates.IsDefaultOrEmpty ||
            window.PriorExchangeSessionDates.Length != policy.HistoricalLookbackSessions)
            throw new ArgumentException("The exact prior-session window must match the configured lookback.", nameof(window));
        if (window.PriorExchangeSessionDates.Distinct().Count() != window.PriorExchangeSessionDates.Length ||
            window.PriorExchangeSessionDates.Any(date => date >= window.CurrentExchangeValueDate))
            throw new ArgumentException("Prior exchange-session dates must be unique and precede the current value date.", nameof(window));
        if (current.ExchangeValueDate != window.CurrentExchangeValueDate)
            throw new ArgumentException("The current observation must match the window current value date.", nameof(current));
    }
}
