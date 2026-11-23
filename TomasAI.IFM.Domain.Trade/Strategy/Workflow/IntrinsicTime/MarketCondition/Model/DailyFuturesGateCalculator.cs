using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Market permission only: fixed existing ITI thresholds and exit owners remain authoritative.</summary>
public static class DailyFuturesGateCalculator
{
    internal static readonly string[] SourceIds = ["ReferenceQuote", "LastTrade", "FeedHealth", "SessionCalendar", "EventRiskCalendar", "Atr14", "AtrBaseline", "AtrBaselineRatio"];

    internal static bool IsEntryWindow(IMarketSessionCalendar calendar, DateTime at)
    {
        var date = calendar.GetValueDate(new DateTimeOffset(at));
        if (!calendar.IsTradingDate(date)) return false;
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(9, 30)), eastern);
        return at >= start && at < EntryCutoff(calendar, date);
    }

    static DateTime EntryCutoff(IMarketSessionCalendar calendar, DateOnly date)
    {
        var eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var cutoff = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(15, 55)), eastern);
        var earlyCutoff = calendar.GetSession(date).EndUtc.UtcDateTime.AddMinutes(-5);
        return earlyCutoff < cutoff ? earlyCutoff : cutoff;
    }

    public static MarketConditionAssessmentResult Calculate(ExecuteMarketConditionAssessmentCommand c,
        MarketConditionAssessmentSnapshot s, Guid resultId, IMarketSessionCalendar calendar, bool prepared = false)
    {
        var regime = prepared ? c.RegimeResultEnvelope.RegimeResult! : MarketConditionAssessmentContracts.ValidateRequest(c);
        if (!DailyFuturesGatePolicy.Matches(c.WorkflowView) || !DailyFuturesGatePolicy.IsSupported(c.CalculationPolicy))
            throw new ArgumentException("Daily futures policy requires a matching assigned Daily futures EMA workflow.", nameof(c));
        var legacy = c.CalculationPolicy == DailyFuturesGatePolicy.LegacyVersion;
        var marketOnly = c.CalculationPolicy == DailyFuturesGatePolicy.Version;
        var p = c.ParameterSet;
        var at = s.EvaluatedAtUtc;
        if (s.SchemaVersion != 1 || resultId == Guid.Empty || s.SnapshotId == Guid.Empty || !MarketConditionAssessmentContracts.Utc(at)
            || at < c.RequestedAtUtc || at >= c.ExpiresAtUtc || s.TargetHorizon != TimeFrameType.Daily
            || s.MarketProfileId != p.MarketProfileId || s.InstrumentRoot != p.InstrumentRoot || !prepared && s.PayloadSha256 != s.ComputeHash()
            || s.Observations.Length != SourceIds.Length || !s.Observations.Select(o => o.SourceId).Order().SequenceEqual(SourceIds.Order()))
            throw new ArgumentException("Invalid Daily futures snapshot identity, seal or source set.", nameof(s));
        var reasons = new List<string>();
        var evidence = new List<AssessmentEvidence>();
        var expiry = new[] { at.AddSeconds(p.HorizonProfile.ResultLifetimeSeconds), c.ExpiresAtUtc,
            regime.ProducedAtUtc.AddSeconds(p.HorizonProfile.RegimeMaximumAgeSeconds) }.Min();
        var date = calendar.GetValueDate(new DateTimeOffset(at));
        if (!marketOnly && calendar.IsTradingDate(date)) expiry = new[] { expiry, EntryCutoff(calendar, date) }.Min();
        void Reject(string code) { var reason = DailyFuturesGatePolicy.Prefix + code; if (!reasons.Contains(reason)) reasons.Add(reason); }
        if (s.ReferenceInstrumentId != c.TriggerEvent.EntityId.ContractId || c.TriggerEvent.FuturesItiSignal?.ContractId != s.ReferenceInstrumentId)
            Reject("SIGNAL_IDENTITY_MISMATCH");
        var trigger = c.TriggerEvent;
        var signal = trigger.FuturesItiSignal;
        var signalAt = signal?.IntrinsicTime ?? default;
        if (!MarketConditionAssessmentContracts.Utc(signalAt) || signalAt > at.AddSeconds(p.FutureClockSkewSeconds)
            || at - signalAt >= TimeSpan.FromSeconds(p.TriggerMaximumAgeSeconds)
            || !MarketConditionAssessmentContracts.Utc(trigger.CreatedOn) || trigger.CreatedOn > at.AddSeconds(p.FutureClockSkewSeconds)
            || at - trigger.CreatedOn >= TimeSpan.FromSeconds(p.TriggerMaximumAgeSeconds)) Reject("SIGNAL_STALE");
        if (!marketOnly && signal?.IntrinsicTimeMode != IntrinsicTimeModeType.TrendDirectionChanged) Reject("NOT_DC_EVENT");
        if (!marketOnly && (!IsEntryWindow(calendar, at) || s.SessionState != MarketSessionStatus.Open)) Reject("OUTSIDE_ENTRY_WINDOW");
        var minimum = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FiveMinutes, 1, new DateTimeOffset(at).AddSeconds(-30), calendar)[0].StartUtc.UtcDateTime;
        if (regime.ProducedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds) || regime.MarketDataAsOfUtc > at.AddSeconds(p.FutureClockSkewSeconds)
            || regime.MarketDataAsOfUtc < minimum || regime.ProducedAtUtc.AddSeconds(p.HorizonProfile.RegimeMaximumAgeSeconds) <= at) Reject("REGIME_STALE");
        var direction = regime.Decision.Direction;
        var alignment = marketOnly ? AssessmentTriggerAlignment.NotApplicable : direction == RegimeDirection.Neutral ? AssessmentTriggerAlignment.Neutral
            : (direction == RegimeDirection.Up && signal?.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
                || direction == RegimeDirection.Down && signal?.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend)
                ? AssessmentTriggerAlignment.Aligned : AssessmentTriggerAlignment.Conflicted;
        if (!marketOnly && direction == RegimeDirection.Neutral) Reject("RANGEBOUND");
        else if (!marketOnly && alignment != AssessmentTriggerAlignment.Aligned) Reject("DIRECTION_MISMATCH");
        foreach (var o in s.Observations)
        {
            if (!legacy && o.SourceId is "AtrBaseline" or "AtrBaselineRatio")
            {
                evidence.Add(new(TimeFrameType.Daily, o.SourceId, "OptionalContext", o.Value, o.Unit, o.ObservedAtUtc,
                    Math.Max(0, (decimal)(at - o.ObservedAtUtc).TotalSeconds), o.Availability, o.Reason, o.Sequence));
                continue;
            }
            var atr = o.SourceId.StartsWith("Atr", StringComparison.Ordinal);
            var age = at - o.ObservedAtUtc;
            var usable = o.Availability == MarketSourceAvailability.Available && o.Validity == MarketSourceValidity.Valid
                && MarketConditionAssessmentCalculator.HasTrustworthyMetadata(o, at, p.FutureClockSkewSeconds);
            if (atr)
            {
                if (!usable || o.Value is not > 0) Reject("ATR_NOT_READY");
                else if (o.ObservedAtUtc < minimum) Reject("ATR_STALE");
                else
                {
                    var barStart = new DateTime(o.ObservedAtUtc.Ticks - o.ObservedAtUtc.Ticks % TimeSpan.FromMinutes(5).Ticks, DateTimeKind.Utc);
                    expiry = new[] { expiry, barStart.AddMinutes(10).AddSeconds(30) }.Min();
                }
            }
            else
            {
                var binding = p.Sources.Single(b => b.SourceId == o.SourceId);
                usable &= age < TimeSpan.FromSeconds(binding.MaximumAgeSeconds);
                if (!usable) Reject(o.SourceId switch { "ReferenceQuote" => "QUOTE_UNAVAILABLE", "LastTrade" => "TRADE_UNAVAILABLE",
                    "FeedHealth" => "FEED_UNAVAILABLE", "EventRiskCalendar" => "CALENDAR_UNAVAILABLE", _ => "SESSION_UNAVAILABLE" });
                else expiry = new[] { expiry, o.ObservedAtUtc.AddSeconds(binding.MaximumAgeSeconds) }.Min();
                if (o.SourceId == "LastTrade" && o.Value is not > 0) Reject("TRADE_UNAVAILABLE");
            }
            evidence.Add(new(TimeFrameType.Daily, o.SourceId, "SourceObservation", o.Value, o.Unit, o.ObservedAtUtc,
                Math.Max(0, (decimal)age.TotalSeconds), o.Availability, o.Reason, o.Sequence));
        }
        var atrSources = s.Observations.Where(o => o.SourceId.StartsWith("Atr", StringComparison.Ordinal)).ToArray();
        if (legacy && (atrSources.Select(o => o.ObservedAtUtc).Distinct().Count() != 1
            || atrSources.Select(o => o.Sequence).Distinct().Count() != 1)) Reject("ATR_INCONSISTENT");
        var liquidity = AssessmentLiquidity.Unknown;
        if (s.Quote is not { } q) Reject("QUOTE_UNAVAILABLE");
        else
        {
            var spread = (q.Ask - q.Bid) / p.TickSize;
            var size = Math.Min(q.BidSize, q.AskSize);
            if (q.Bid <= 0 || q.Ask <= 0 || spread < 0 || q.Bid % p.TickSize != 0 || q.Ask % p.TickSize != 0 || legacy && size < 0)
                Reject("QUOTE_INVALID");
            else
            {
                liquidity = spread <= p.HealthySpreadTicks && (!legacy || size >= p.HealthyBestSize) ? AssessmentLiquidity.Healthy
                    : spread <= p.DegradedSpreadTicks && (!legacy || size >= p.DegradedBestSize) ? AssessmentLiquidity.Degraded : AssessmentLiquidity.Poor;
                if (!marketOnly && liquidity == AssessmentLiquidity.Poor) Reject("LIQUIDITY_POOR");
            }
            evidence.Add(new(TimeFrameType.Daily, "ReferenceQuote", "SpreadTicks", spread, "ticks", at, 0, MarketSourceAvailability.Available, legacy ? "Configured ES spread/depth limits" : "Configured ES spread limit; displayed depth is not a Market Condition veto"));
        }
        var coverage = s.CalendarEvidence;
        if (coverage is not { CoverageConfirmed: true, ValidUntilUtc: not null } || coverage.ValidUntilUtc <= at
            || coverage.PolicyVersion != p.CalendarCoveragePolicy || coverage.Country != "US"
            || !MarketConditionAssessmentContracts.Utc(coverage.CheckedAtUtc) || coverage.CheckedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds))
            Reject("CALENDAR_UNAVAILABLE");
        else
        {
            var calendarExpiry = coverage.ValidUntilUtc.Value;
            foreach (var attempt in coverage.Attempts.GroupBy(x => x.Outcome.ValueDate).Select(g => g.OrderByDescending(x => x.Outcome.RequestedAtUtc)
                .ThenBy(x => x.Outcome.Status == TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadStatus.Failed ? 0 : 1)
                .ThenBy(x => x.Outcome.FinishedAtUtc).ThenBy(x => x.Outcome.ImportCommandId).First()))
                calendarExpiry = new[] { calendarExpiry, attempt.Outcome.FinishedAtUtc.AddSeconds(p.CalendarDownloadMaximumAgeSeconds) }.Min();
            if (calendarExpiry <= at) Reject("CALENDAR_UNAVAILABLE");
            expiry = new[] { expiry, calendarExpiry }.Min();
        }
        if (s.EventContext is not (AssessmentEventContext.Clear or AssessmentEventContext.Elevated)) Reject("CALENDAR_UNAVAILABLE");
        else if (!marketOnly && s.EventContext == AssessmentEventContext.Elevated) Reject("EVENT_BLACKOUT");
        if (s.SessionState is not (MarketSessionStatus.Open or MarketSessionStatus.Closed)) Reject("SESSION_UNAVAILABLE");
        if (!marketOnly && regime.Decision.Restrictions.Contains(RegimeRestriction.NoNewTrade)) Reject("UPSTREAM_BLOCKED");
        var eligible = reasons.Count == 0;
        evidence.Add(new(TimeFrameType.Daily, c.CalculationPolicy!, "Policy", null, "", at, 0, MarketSourceAvailability.Available, c.CalculationPolicy!));
        evidence.Add(new(TimeFrameType.Daily, c.CalculationPolicy!, marketOnly ? "Availability" : "Eligibility", eligible ? 1 : 0, "boolean", at, 0,
            MarketSourceAvailability.Available, eligible ? marketOnly ? "MC.ASSESSMENT.AVAILABLE" : DailyFuturesGatePolicy.Prefix + "ELIGIBLE" : reasons[0]));
        var summary = eligible ? marketOnly ? "Daily futures assessment available; required observations are valid; strategy eligibility belongs to Trade Selection." : "Daily futures: Eligible; aligned DC, RTH, current overnight-warmed five-minute ATR and tradable ES quotes."
            : (marketOnly ? "Daily futures assessment unavailable; " : "Daily futures: No Trade; ") + string.Join(", ", reasons);
        var result = new MarketConditionAssessmentResult { ResultId = resultId, WorkflowId = c.WorkflowId, EntityId = c.WorkflowEntityId,
            CommandId = c.CommandId, InputWorkflowRevision = c.InputWorkflowRevision, MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot,
            ParameterSetId = p.ParameterSetId, ParameterSetVersion = p.Version, ParameterPayloadSha256 = c.ParameterPayloadSha256,
            RegimeResultId = regime.ResultId, RegimePayloadSha256 = c.RegimePayloadSha256, SnapshotId = s.SnapshotId, SnapshotSha256 = s.PayloadSha256,
            EvaluatedAtUtc = at, TargetHorizon = TimeFrameType.Daily, CalendarEvidence = coverage, SummaryText = summary,
            Assessment = new() { Horizon = TimeFrameType.Daily, Availability = eligible ? AssessmentAvailability.Available : AssessmentAvailability.Unavailable,
                RegimeResultId = regime.ResultId, RegimePayloadSha256 = c.RegimePayloadSha256, UpstreamContext = eligible ? regime.Decision : null,
                ConditionType = eligible ? marketOnly && direction == RegimeDirection.Neutral ? AssessmentCondition.RangeBound : AssessmentCondition.Directional : null, VolatilityBehavior = AssessmentVolatility.Unknown,
                LiquidityCondition = liquidity, SessionState = s.SessionState, EventRiskState = s.EventContext,
                StressState = eligible ? AssessmentStress.Normal : AssessmentStress.Unknown, TriggerAlignment = alignment,
                AssessmentConfidence = eligible ? regime.Decision.Confidence : null, DataQuality = eligible && !(marketOnly && s.Observations.Any(o => o.SourceId == "FeedHealth" && o.Value == 0)) ? MarketConditionDataQuality.Healthy : MarketConditionDataQuality.Unusable,
                EvaluatedAtUtc = at, ValidUntilUtc = eligible ? expiry : null, EvidenceItems = evidence.ToArray(), LimitationReasons = reasons.ToArray(),
                InheritedRestrictions = regime.Decision.Restrictions.Distinct().Order().ToArray(), SummaryText = summary } };
        if (!prepared) MarketConditionAssessmentContracts.ValidateResult(result);
        return result;
    }
}
