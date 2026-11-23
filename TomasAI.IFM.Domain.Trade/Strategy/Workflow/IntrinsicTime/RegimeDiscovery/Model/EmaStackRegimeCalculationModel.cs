using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Pure, deterministic comparison of three same-observation EMAs; no volatility or trend-age inference.</summary>
public static class EmaStackRegimeCalculationModel
{
    public static RegimeDiscoveryResult Calculate(RegimeDiscoveryCalculationInput input)
    {
        var frame = EmaStackRegimePolicy.Frame(input.ParameterSet.TargetHorizon);
        var metrics = new[] { RegimeDiscoverySignalMetric.Ema20, RegimeDiscoverySignalMetric.Ema50, RegimeDiscoverySignalMetric.Ema200 };
        var observations = metrics.Select(metric => input.Snapshot.Observations.SingleOrDefault(o => o.Metric == metric && o.SignalKey.TimeFrame == frame)).ToArray();
        var ready = observations.All(o => o is { IsWarm: true, IsValid: true, Availability: RegimeDiscoverySignalAvailability.Available, Value: > 0 })
            && observations.Select(o => o!.MarketDataAsOfUtc).Distinct().Count() == 1;
        var up = ready && observations[0]!.Value > observations[1]!.Value && observations[1]!.Value > observations[2]!.Value;
        var down = ready && observations[0]!.Value < observations[1]!.Value && observations[1]!.Value < observations[2]!.Value;
        var label = !ready ? "Inputs unavailable" : up ? "Strongly Bullish" : down ? "Strongly Bearish" : "Rangebound";
        var direction = !ready ? RegimeDirection.Unknown : up ? RegimeDirection.Up : down ? RegimeDirection.Down : RegimeDirection.Neutral;
        var reasons = new[] { new RegimeDiscoveryReason { Code = EmaStackRegimePolicy.ReasonCode, Area = RegimeEvidenceArea.Data, Severity = RegimeReasonSeverity.Information },
            new RegimeDiscoveryReason { Code = !ready ? "RD.EMA.INPUTS_UNAVAILABLE" : up ? "RD.EMA.STRONGLY_BULLISH" : down ? "RD.EMA.STRONGLY_BEARISH" : "RD.EMA.RANGEBOUND",
                Area = RegimeEvidenceArea.Trend, TimeFrame = frame, Severity = ready ? RegimeReasonSeverity.Information : RegimeReasonSeverity.Failure } };
        var evidence = observations.Where(o => o is not null).Select(o => new RegimeDiscoveryEvidence { Area = RegimeEvidenceArea.Trend,
            EvidenceId = o!.Metric.ToString(), SignalKind = MarketAnalyticsSignalKind.Ema, TimeFrame = frame, Value = o.Value,
            Weight = 1m, FreshnessFactor = o.FreshnessFactor, IsAvailable = ready, IsRequired = true,
            MarketDataAsOfUtc = o.MarketDataAsOfUtc, SignalIdentity = o.SignalIdentity }).ToArray();
        // Confidence represents complete deterministic input coverage, not a forecast win probability.
        var coverage = ready ? 1m : 0m;
        var strength = up || down ? TrendRegimeStrength.Strong : TrendRegimeStrength.None;
        var structure = !ready ? MarketStructureClassification.Unknown : up || down ? MarketStructureClassification.Trending : MarketStructureClassification.Ranging;
        var decision = new RegimeDiscoveryDecision { IsComplete = ready, Direction = direction,
            DirectionalScore = up ? 1 : down ? -1 : 0, Confidence = coverage, Quality = ready ? RegimeOverallQuality.High : RegimeOverallQuality.Unknown,
            ConfidenceBand = ready ? RegimeConfidenceBand.VeryHigh : RegimeConfidenceBand.Unknown, TrendStrength = strength,
            StructureClassification = structure, Reasons = reasons };
        return new() { ResultId = input.ResultId, WorkflowId = input.WorkflowId, EntityId = input.EntityId, TriggerEventId = input.TriggerEventId,
            StrategyParameterSetId = input.ParameterSet.StrategyParameterSetId, StrategyParameterSetVersion = input.ParameterSet.StrategyParameterSetVersion,
            RegimeDiscoveryParameterSetId = input.ParameterSet.ParameterSetId, RegimeDiscoveryParameterSetVersion = input.ParameterSet.Version,
            TargetHorizon = input.ParameterSet.TargetHorizon, SignalSnapshotId = input.Snapshot.SnapshotId,
            ProducedAtUtc = input.ProducedAtUtc, MarketDataAsOfUtc = input.Snapshot.MarketDataAsOfUtc,
            Trend = new() { IsComplete = ready, Direction = direction, Strength = strength, Confidence = coverage, Evidence = evidence, Reasons = reasons },
            Decision = decision, SupportingEvidence = evidence, Reasons = reasons, OverallQuality = decision.Quality, OverallConfidence = coverage,
            SummaryText = $"{input.ParameterSet.TargetHorizon}: {label}; {frame} EMA20/50/200; Policy={EmaStackRegimePolicy.Version}." };
    }
}
