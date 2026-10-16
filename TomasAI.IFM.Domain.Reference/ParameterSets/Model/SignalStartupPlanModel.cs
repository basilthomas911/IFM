using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Pure demand union. Keeps existing consumers and exact period identities; readiness never starts work.</summary>
public static class SignalStartupPlanModel
{
    public static readonly TimeFrameType[] SupportedIntervals = [TimeFrameType.FifteenSeconds,TimeFrameType.OneMinute,
  TimeFrameType.FiveMinutes,TimeFrameType.FifteenMinutes,TimeFrameType.OneHour,TimeFrameType.FourHours,TimeFrameType.Daily];
    public static ParameterSignalStartupPlan Create(ParameterStartupSnapshotModel snapshot,
     IEnumerable<ParameterSignalDemand> existingConsumers)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var issues = new List<string>();
        var demands = existingConsumers.Concat(snapshot.Assignments
            .Where(x => string.Equals(x.Assignment.Scope.ComponentCode,RegimeDiscoveryParameterModel.ComponentCode,StringComparison.Ordinal))
            .SelectMany(x => Expand(x, issues)))
         .SelectMany(demand => new[] { demand }.Concat(Dependencies(demand.Key).Select(key => demand with { Key = key }))).ToArray();
        foreach (var demand in demands) ValidateDemand(demand);
        var steps = demands.GroupBy(x => x.Key).OrderBy(x => x.Key.Producer).ThenBy(x => x.Key.Interval).ThenBy(x => x.Key.Period)
         .Select(group => new ParameterSignalStartupStep(group.Key, group.Any(x => x.Prepare), group.Any(x => x.Monitor),
          group.Distinct().OrderBy(x => x.Consumer, StringComparer.Ordinal).ThenBy(x => x.RequirementId).ToArray())).ToArray();
        var fingerprint = ParameterBinaryFingerprint.Hash((snapshot.Fingerprint,steps,issues.ToArray()));
        return new(snapshot.StartupRunId, snapshot.Fingerprint, fingerprint, steps, issues.Count == 0 ? null : issues.ToArray());
    }
    public static IEnumerable<ParameterSignalDemand> ExistingIntradayConsumers()
    {
        foreach (var interval in FuturesIntradaySignalActivationProfile.TimeFrames)
            foreach (var key in new[]{new ParameterSignalProducerKey(ParameterSignalProducer.Rsi,interval,FuturesIntradaySignalActivationProfile.RsiPeriodLength),
   new(ParameterSignalProducer.Atr,interval,14),new(ParameterSignalProducer.Adx,interval,14),new(ParameterSignalProducer.Macd,interval)})
                yield return new(key, "existing-intraday-activation", Guid.Empty, true, false, int.MaxValue);
    }
    static void ValidateDemand(ParameterSignalDemand demand)
    {
        ArgumentNullException.ThrowIfNull(demand);
        if (!SupportedIntervals.Contains(demand.Key.Interval) || !Enum.IsDefined(demand.Key.Producer) ||
            string.IsNullOrWhiteSpace(demand.Consumer) || demand.MaximumAgeSeconds <= 0)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
            throw new ArgumentException("SIGNAL.STARTUP_DEMAND_INVALID");
#pragma warning restore MA0015
    }

    static IEnumerable<ParameterSignalDemand> Expand(AppliedParameterAssignment applied, List<string> issues)
    {
        var value = applied.Version.Values().Get<RegimeDiscoveryParameterSet>();
        var rows = value.SchemaVersion == 1 ? RegimeDiscoveryParameterModel.Defaults(value) : value.SignalRequirements!;
        foreach (var row in rows.Where(x => x.Enabled))
        {
            var consumer = applied.Assignment.AssignmentId.ToString("N") + ":" + applied.Assignment.Revision;
            ParameterSignalProducerKey key;
            try { key = Producer(row.Metric, row.TimeFrame); }
            catch (ArgumentException) when (value.SchemaVersion < 3 && !row.IsRequired)
            {
                issues.Add($"SIGNAL.LEGACY_OPTIONAL_PRODUCER_UNSUPPORTED: {consumer}: {row.RequirementId}: {row.Metric}/{row.TimeFrame}; no producer started.");
                continue;
            }
            yield return new(key, consumer, row.RequirementId, row.PrepareAtStartup, row.Monitor, row.MaximumAgeSeconds);
        }
    }
    public static IEnumerable<ParameterSignalProducerKey> Dependencies(ParameterSignalProducerKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return EnumerateDependencies(key);
    }

    static IEnumerable<ParameterSignalProducerKey> EnumerateDependencies(ParameterSignalProducerKey key)
    {
        if (key.Producer is ParameterSignalProducer.ClosedBars or ParameterSignalProducer.VxTermStructure) yield break;
        yield return new(ParameterSignalProducer.ClosedBars, key.Interval);
        if (key.Producer == ParameterSignalProducer.Tdi) yield return new(ParameterSignalProducer.Rsi, key.Interval, 13);
        if (key.Producer == ParameterSignalProducer.Structure)
        {
            yield return new(ParameterSignalProducer.Atr, key.Interval, 14);
            yield return new(ParameterSignalProducer.Ema, key.Interval);
        }
    }
    public static ParameterSignalProducerKey Producer(RegimeDiscoverySignalMetric metric, TimeFrameType interval)
    {
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
        if (!SupportedIntervals.Contains(interval)) throw new ArgumentException("SIGNAL.INTERVAL_UNSUPPORTED");
#pragma warning restore MA0015
        return metric switch
        {
            RegimeDiscoverySignalMetric.Ema20 or RegimeDiscoverySignalMetric.Ema50 or RegimeDiscoverySignalMetric.Ema200 or
             RegimeDiscoverySignalMetric.Ema20Slope or RegimeDiscoverySignalMetric.Ema50Slope or RegimeDiscoverySignalMetric.Ema200Slope => new(ParameterSignalProducer.Ema, interval),
            RegimeDiscoverySignalMetric.Rsi14 or RegimeDiscoverySignalMetric.Rsi14Slope => new(ParameterSignalProducer.Rsi, interval, 14),
            RegimeDiscoverySignalMetric.Atr14 or RegimeDiscoverySignalMetric.AtrBaselineRatio => new(ParameterSignalProducer.Atr, interval, 14),
            RegimeDiscoverySignalMetric.Adx14 or RegimeDiscoverySignalMetric.PlusDi14 or RegimeDiscoverySignalMetric.MinusDi14 => new(ParameterSignalProducer.Adx, interval, 14),
            RegimeDiscoverySignalMetric.MacdHistogram => new(ParameterSignalProducer.Macd, interval),
            RegimeDiscoverySignalMetric.BollingerWidth or RegimeDiscoverySignalMetric.BollingerWidthRatio or RegimeDiscoverySignalMetric.BollingerPosition => new(ParameterSignalProducer.Bollinger, interval),
            RegimeDiscoverySignalMetric.Ema20Interaction or RegimeDiscoverySignalMetric.AtrNormalizedRange or RegimeDiscoverySignalMetric.RollingHigh20 or
             RegimeDiscoverySignalMetric.RollingLow20 or RegimeDiscoverySignalMetric.BreakoutDistanceAtr => new(ParameterSignalProducer.Structure, interval),
            RegimeDiscoverySignalMetric.Tdi => new(ParameterSignalProducer.Tdi, interval),
            RegimeDiscoverySignalMetric.VxFrontSecondRatio when interval == TimeFrameType.Daily => new(ParameterSignalProducer.VxTermStructure, interval),
            _ => throw new ArgumentException("SIGNAL.PRODUCER_UNSUPPORTED: " + metric + "/" + interval,nameof(metric))
        };
    }
}
