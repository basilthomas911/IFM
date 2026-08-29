using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Provides deterministic normalized arithmetic shared by Regime Discovery models.</summary>
internal static class RegimeDiscoveryMath
{
    internal const int DecimalPlaces = 6;

    internal static decimal Round(decimal value) =>
        Math.Round(value, DecimalPlaces, MidpointRounding.ToEven);

    internal static decimal Clamp(decimal value, decimal minimum = 0m, decimal maximum = 1m) =>
        Round(Math.Min(maximum, Math.Max(minimum, value)));

    internal static decimal SignedClamp(decimal value) => Clamp(value, -1m, 1m);

    internal static decimal Sign(decimal value) => value > 0m ? 1m : value < 0m ? -1m : 0m;

    internal static RegimeConfidenceBand ConfidenceBand(decimal confidence) => confidence switch
    {
        < 0.35m => RegimeConfidenceBand.Low,
        < 0.60m => RegimeConfidenceBand.Moderate,
        < 0.80m => RegimeConfidenceBand.High,
        _ => RegimeConfidenceBand.VeryHigh
    };

    internal static decimal WeightedScore(IEnumerable<WeightedValue> values)
    {
        var included = values.Where(value => value.IsAvailable && value.Weight > 0m).ToArray();
        var totalWeight = included.Sum(value => value.Weight);
        return totalWeight <= 0m
            ? 0m
            : Round(included.Sum(value => value.Value * value.Weight) / totalWeight);
    }

    internal static ConfidenceValues Confidence(IEnumerable<WeightedValue> values)
    {
        var enabled = values.Where(value => value.Weight > 0m).ToArray();
        var totalWeight = enabled.Sum(value => value.Weight);
        if (totalWeight <= 0m) return new(0m, 0m, 0m, 0m);
        var available = enabled.Where(value => value.IsAvailable).ToArray();
        var availableWeight = available.Sum(value => value.Weight);
        if (availableWeight <= 0m) return new(0m, 0m, 0m, 0m);
        var score = WeightedScore(available);
        var coverage = Clamp(availableWeight / totalWeight);
        var freshness = Clamp(available.Sum(value => value.Weight * value.FreshnessFactor) / availableWeight);
        var disagreement = available.Sum(value => value.Weight * Math.Abs(value.Value - score)) /
                           (2m * availableWeight);
        var agreement = Clamp(1m - disagreement);
        var confidence = Clamp(coverage * (0.45m * agreement + 0.35m * freshness + 0.20m));
        return new(coverage, freshness, agreement, confidence);
    }

    internal static decimal Piecewise(decimal value, params (decimal X, decimal Y)[] points)
    {
        if (points.Length < 2) throw new ArgumentException("At least two piecewise points are required.", nameof(points));
        var ordered = points.OrderBy(point => point.X).ToArray();
        if (value <= ordered[0].X) return Round(ordered[0].Y);
        if (value >= ordered[^1].X) return Round(ordered[^1].Y);
        for (var index = 1; index < ordered.Length; index++)
        {
            if (value > ordered[index].X) continue;
            var lower = ordered[index - 1];
            var upper = ordered[index];
            var fraction = (value - lower.X) / (upper.X - lower.X);
            return Round(lower.Y + fraction * (upper.Y - lower.Y));
        }
        return Round(ordered[^1].Y);
    }

    internal static RegimeDiscoverySignalObservation? Find(
        RegimeDiscoveryMarketSignalSnapshot snapshot,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame) => snapshot.Observations.FirstOrDefault(observation =>
            observation.Metric == metric && observation.SignalKey.TimeFrame == timeFrame);

    internal static RegimeDiscoverySignalObservation? Find(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType timeFrame)
    {
        var observation = Find(input.Snapshot, metric, timeFrame);
        if (timeFrame != input.ParameterSet.TargetHorizon ||
            input.TriggerEvent?.FuturesItiSignal is not { } trigger)
            return observation;

        decimal? authoritativeValue = metric switch
        {
            RegimeDiscoverySignalMetric.CurrentPrice => (decimal)trigger.IntrinsicPrice,
            RegimeDiscoverySignalMetric.ItiDirection => trigger.IntrinsicTimeTrend switch
            {
                IntrinsicTimeTrendType.UpTrend => 1m,
                IntrinsicTimeTrendType.DownTrend => -1m,
                _ => 0m
            },
            RegimeDiscoverySignalMetric.ItiBandLevel => (decimal)trigger.BandLevel,
            RegimeDiscoverySignalMetric.ItiReversalLevel => (decimal)trigger.ReversalLevel,
            RegimeDiscoverySignalMetric.VxFrontLevel when input.TriggerEvent.VixFuturesPrice > 0 =>
                (decimal)input.TriggerEvent.VixFuturesPrice,
            _ => null
        };
        if (authoritativeValue is null)
            return observation;
        if (observation is null)
            return null;

        var marketDataAsOfUtc = DateTime.SpecifyKind(trigger.IntrinsicTime, DateTimeKind.Utc);
        var calculatedAtUtc = input.TriggerEvent.CreatedOn == default
            ? input.TriggerEvent.ReceivedOn
            : input.TriggerEvent.CreatedOn;
        return observation with
        {
            Value = authoritativeValue.Value,
            MarketDataAsOfUtc = marketDataAsOfUtc,
            CalculatedAtUtc = calculatedAtUtc,
            SourceSequence = trigger.SequenceId,
            SignalIdentity = $"Trigger.{input.TriggerEventId}.{metric}.{timeFrame}"
        };
    }

    internal static RegimeDiscoverySignalObservation? FindAny(
        RegimeDiscoveryMarketSignalSnapshot snapshot,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType preferredTimeFrame) =>
        Find(snapshot, metric, preferredTimeFrame) ??
        snapshot.Observations.FirstOrDefault(observation => observation.Metric == metric);

    internal static RegimeDiscoverySignalObservation? FindAny(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoverySignalMetric metric,
        TimeFrameType preferredTimeFrame) =>
        Find(input, metric, preferredTimeFrame) ??
        input.Snapshot.Observations.FirstOrDefault(observation => observation.Metric == metric);

    internal static bool IsAvailable(RegimeDiscoverySignalObservation? observation) =>
        observation is
        {
            Availability: RegimeDiscoverySignalAvailability.Available,
            IsWarm: true,
            IsValid: true
        };

    internal static RegimeDiscoveryEvidence Evidence(
        RegimeEvidenceArea area,
        string evidenceId,
        RegimeDiscoverySignalObservation? observation,
        decimal normalizedValue,
        decimal weight,
        bool required) => new()
        {
            Area = area,
            EvidenceId = evidenceId,
            SignalKind = observation?.SignalKey.SignalKind ?? 0,
            TimeFrame = observation?.SignalKey.TimeFrame ?? TimeFrameType.None,
            Value = Round(normalizedValue),
            Weight = Round(weight),
            FreshnessFactor = observation?.FreshnessFactor ?? 0m,
            IsRequired = required,
            IsAvailable = IsAvailable(observation),
            MarketDataAsOfUtc = observation?.MarketDataAsOfUtc ?? default,
            SignalIdentity = observation?.SignalIdentity ?? string.Empty
        };

    internal static RegimeDiscoveryReason Reason(
        string code,
        RegimeReasonSeverity severity,
        RegimeEvidenceArea area,
        RegimeDiscoverySignalObservation? observation = null) => new()
        {
            Code = code,
            Severity = severity,
            Area = area,
            TimeFrame = observation?.SignalKey.TimeFrame ?? TimeFrameType.None,
            SignalIdentity = observation?.SignalIdentity ?? string.Empty
        };

    internal static RegimeDiscoveryReason MissingReason(
        RegimeEvidenceArea area,
        RegimeDiscoverySignalObservation? observation,
        bool required) => Reason(
        required ? RegimeDiscoveryReasonCodes.RequiredDataMissing : RegimeDiscoveryReasonCodes.OptionalDataMissing,
        required ? RegimeReasonSeverity.Failure : RegimeReasonSeverity.Warning,
        area,
        observation);

    internal static RegimeDiscoveryEvidence[] OrderEvidence(IEnumerable<RegimeDiscoveryEvidence> evidence) =>
        evidence.OrderBy(item => item.Area)
            .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ThenBy(item => item.TimeFrame)
            .ThenBy(item => item.SignalIdentity, StringComparer.Ordinal)
            .ToArray();

    internal static RegimeDiscoveryReason[] OrderReasons(IEnumerable<RegimeDiscoveryReason> reasons) =>
        reasons.DistinctBy(item => (item.Code, item.TimeFrame, item.SignalIdentity))
            .OrderBy(item => item.Area)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.TimeFrame)
            .ThenBy(item => item.SignalIdentity, StringComparer.Ordinal)
            .ToArray();
}

internal readonly record struct WeightedValue(
    decimal Value,
    decimal Weight,
    decimal FreshnessFactor,
    bool IsAvailable = true);

internal readonly record struct ConfidenceValues(
    decimal Coverage,
    decimal Freshness,
    decimal Agreement,
    decimal Confidence);
