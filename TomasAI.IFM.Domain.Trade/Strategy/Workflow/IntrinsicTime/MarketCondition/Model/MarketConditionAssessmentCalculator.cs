using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

/// <summary>Pure evaluation of one sealed market snapshot and its accepted upstream decision.</summary>
public sealed class MarketConditionAssessmentCalculator
{
    public MarketConditionAssessmentResult Calculate(ExecuteMarketConditionAssessmentCommand command,
        MarketConditionAssessmentSnapshot snapshot, Guid resultId)
    {
        var regime = MarketConditionAssessmentContracts.ValidateRequest(command);
        var p = command.ParameterSet;
        var s = snapshot;
        var at = s.EvaluatedAtUtc;
        if (s.SchemaVersion != 1 || s.SnapshotId == Guid.Empty || resultId == Guid.Empty ||
            s.MarketProfileId != p.MarketProfileId || s.InstrumentRoot != p.InstrumentRoot || s.TargetHorizon != p.TargetHorizon ||
            !MarketConditionAssessmentContracts.Utc(at) || at < command.RequestedAtUtc || at >= command.ExpiresAtUtc ||
            s.PayloadSha256 != s.ComputeHash() || !Enum.IsDefined(s.SessionState) || !Enum.IsDefined(s.EventContext))
            throw Invalid("Snapshot identity, seal, time or classification is invalid.");
        var observations = s.Observations;
        if (observations.Length != p.Sources.Length || observations.Select(x => x.SourceId).Distinct().Count() != observations.Length ||
            observations.Any(x => !p.Sources.Any(y => y.SourceId == x.SourceId))) throw Invalid("Snapshot source set is invalid.");
        var reasons = new List<string>();
        var evidence = new List<AssessmentEvidence>();
        var fresh = new Dictionary<string, bool>(StringComparer.Ordinal);
        var expiry = at.AddSeconds(p.HorizonProfile.ResultLifetimeSeconds);
        var fitness = 1m;
        foreach (var binding in p.Sources)
        {
            var o = observations.Single(x => x.SourceId == binding.SourceId);
            if (!Enum.IsDefined(o.Availability) || !Enum.IsDefined(o.Validity) || o.Sequence < 0 ||
                o.Availability == MarketSourceAvailability.Unknown || o.Validity != MarketSourceValidity.Valid ||
                !MarketConditionAssessmentContracts.Utc(o.ObservedAtUtc) || !MarketConditionAssessmentContracts.Utc(o.ReceivedAtUtc) ||
                o.ObservedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds) || o.ReceivedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds) ||
                o.ReceivedAtUtc < o.ObservedAtUtc.AddSeconds(-p.FutureClockSkewSeconds)) throw Invalid($"Untrustworthy {o.SourceId} metadata.");
            var age = Age(at, o.ObservedAtUtc);
            var usable = o.Availability == MarketSourceAvailability.Available && age < binding.MaximumAgeSeconds;
            fresh.Add(o.SourceId, usable);
            var reason = usable ? o.Reason : string.IsNullOrWhiteSpace(o.Reason)
                ? $"MC.ASSESSMENT.{o.SourceId.ToUpperInvariant()}.{(o.Availability == MarketSourceAvailability.Unavailable ? "MISSING" : "STALE")}" : o.Reason;
            evidence.Add(new(p.TargetHorizon, o.SourceId, "SourceObservation", o.Value, o.Unit, o.ObservedAtUtc, age, o.Availability, reason,o.Sequence));
            evidence.Add(new(p.TargetHorizon, o.SourceId, "MaximumAge", binding.MaximumAgeSeconds, "seconds", at, 0, MarketSourceAvailability.Available, "Frozen source threshold"));
            if (!usable) reasons.Add(reason);
            if (!binding.Required) continue;
            fitness = Math.Min(fitness, Math.Clamp(1m - age / binding.MaximumAgeSeconds, 0m, 1m));
            expiry = Min(expiry, o.ObservedAtUtc.AddSeconds(binding.MaximumAgeSeconds));
        }
        if (regime.ProducedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds) || regime.MarketDataAsOfUtc > regime.ProducedAtUtc.AddSeconds(p.FutureClockSkewSeconds))
            throw Invalid("Upstream result timestamps are untrustworthy.");
        var regimeAge = Age(at, regime.ProducedAtUtc);
        var requiredFreshness=fitness;
        var regimeFreshness=Math.Clamp(1m-regimeAge/p.HorizonProfile.RegimeMaximumAgeSeconds,0m,1m);
        fitness = Math.Min(fitness, Math.Clamp(1m - regimeAge / p.HorizonProfile.RegimeMaximumAgeSeconds, 0m, 1m));
        expiry = Min(expiry, regime.ProducedAtUtc.AddSeconds(p.HorizonProfile.RegimeMaximumAgeSeconds));
        if (regimeAge >= p.HorizonProfile.RegimeMaximumAgeSeconds) reasons.Add("MC.ASSESSMENT.REGIME.STALE");
        evidence.Add(new(p.TargetHorizon, "AcceptedRegime", "Confidence", regime.Decision.Confidence, "ratio", regime.ProducedAtUtc, regimeAge, MarketSourceAvailability.Available, "Accepted workflow result"));

        var coverage = s.CalendarEvidence;
        if (coverage is not null && (coverage.PolicyVersion != p.CalendarCoveragePolicy || coverage.Country != "US" ||
            !MarketConditionAssessmentContracts.Utc(coverage.CheckedAtUtc) || coverage.CheckedAtUtc > at.AddSeconds(p.FutureClockSkewSeconds) ||
            coverage.ValidUntilUtc is { } until && !MarketConditionAssessmentContracts.Utc(until)))
            throw Invalid("Calendar coverage policy or timestamp does not match the frozen source binding.");
        var covered = coverage is { CoverageConfirmed: true, ValidUntilUtc: not null } && coverage.ValidUntilUtc > at;
        if (covered)
        {
            expiry = Min(expiry, coverage!.ValidUntilUtc!.Value);
            // A profile can demand a stricter download age than the shared coverage reader.
            foreach (var attempt in coverage.Attempts.GroupBy(x=>x.Outcome.ValueDate).Select(group=>group
                .OrderByDescending(x=>x.Outcome.RequestedAtUtc)
                .ThenBy(x=>x.Outcome.Status==TomasAI.IFM.Domain.MarketData.Shared.DownloadLog.MarketDataDownloadStatus.Failed?0:1)
                .ThenBy(x=>x.Outcome.FinishedAtUtc).ThenBy(x=>x.Outcome.ImportCommandId).First()))
                expiry = Min(expiry, attempt.Outcome.FinishedAtUtc.AddSeconds(p.CalendarDownloadMaximumAgeSeconds));
            covered = expiry > at;
        }
        if (!covered) reasons.Add("MC.ASSESSMENT.CALENDAR.COVERAGE_UNCONFIRMED");
        var available = p.Sources.Where(x => x.Required).All(x => fresh[x.SourceId]) &&
            regimeAge < p.HorizonProfile.RegimeMaximumAgeSeconds && covered && expiry > at;
        if (fresh["ReferenceQuote"] && (s.Quote is null || string.IsNullOrWhiteSpace(s.ReferenceInstrumentId)))
            throw Invalid("Available reference quote has no price or contract identity.");
        if (fresh["SessionCalendar"] && s.SessionState == MarketSessionStatus.Unknown ||
            fresh["EventRiskCalendar"] && s.EventContext == AssessmentEventContext.Unknown)
            throw Invalid("Available calendar source has no classification.");
        var liquidity = AssessmentLiquidity.Unknown;
        var crossed = false;
        if (fresh["ReferenceQuote"] && s.Quote is { } q)
        {
            if (q.Bid <= 0 || q.Ask <= 0 || q.BidSize < 0 || q.AskSize < 0 || q.Bid % p.TickSize != 0 || q.Ask % p.TickSize != 0)
                throw Invalid("Quote prices, sizes or tick alignment are invalid.");
            var spread = (q.Ask - q.Bid) / p.TickSize;
            var size = Math.Min(q.BidSize, q.AskSize);
            crossed = spread < 0;
            if (crossed) reasons.Add("MC.ASSESSMENT.QUOTE.CROSSED");
            else
            {
                liquidity = spread <= p.HealthySpreadTicks && size >= p.HealthyBestSize ? AssessmentLiquidity.Healthy
                    : spread <= p.DegradedSpreadTicks && size >= p.DegradedBestSize ? AssessmentLiquidity.Degraded : AssessmentLiquidity.Poor;
                if (spread == 0) reasons.Add("MC.ASSESSMENT.QUOTE.LOCKED");
            }
            var o = observations.Single(x => x.SourceId == "ReferenceQuote");
            evidence.Add(new(p.TargetHorizon, o.SourceId, "SpreadTicks", spread, "ticks", o.ObservedAtUtc, Age(at,o.ObservedAtUtc), o.Availability, crossed ? "Crossed market" : ""));
            evidence.Add(new(p.TargetHorizon, o.SourceId, "BestSideSize", size, "contracts", o.ObservedAtUtc, Age(at,o.ObservedAtUtc), o.Availability, ""));
        }
        decimal? Optional(string id)
        {
            if (!fresh[id]) return null;
            return observations.Single(x => x.SourceId == id).Value ?? throw Invalid($"Available {id} has no value.");
        }
        var movement = Optional("NormalizedMovement");
        var vx = Optional("VolatilityChange");
        if (movement < 0) throw Invalid("Normalized absolute movement cannot be negative.");
        var stress = crossed || movement > p.MovementStressThreshold || vx > p.VolatilityChangeStressThreshold ? AssessmentStress.Elevated
            : movement is not null && vx is not null ? AssessmentStress.Normal : AssessmentStress.Unknown;
        var decision = MessagePackSerializer.Deserialize<RegimeDiscoveryDecision>(MessagePackSerializer.Serialize(regime.Decision));
        AssessmentCondition? condition = !available ? null : stress == AssessmentStress.Elevated ? AssessmentCondition.Dislocated
            : decision.StructureClassification == MarketStructureClassification.Transitioning || decision.Restrictions.Contains(RegimeRestriction.Transition) ? AssessmentCondition.Transition
            : decision.VolatilityChange == VolatilityRegimeChange.Expanding ? AssessmentCondition.VolatilityExpansion
            : decision.VolatilityChange == VolatilityRegimeChange.Contracting ? AssessmentCondition.VolatilityContraction
            : decision.StructureClassification == MarketStructureClassification.Ranging && decision.Direction == RegimeDirection.Neutral ? AssessmentCondition.RangeBound
            : decision.Direction is RegimeDirection.Up or RegimeDirection.Down ? AssessmentCondition.Directional : AssessmentCondition.Unclassified;
        var triggerAt = command.TriggerEvent.CreatedOn != default ? command.TriggerEvent.CreatedOn : command.TriggerEvent.ReceivedOn;
        if (!MarketConditionAssessmentContracts.Utc(triggerAt) || triggerAt > at.AddSeconds(p.FutureClockSkewSeconds)) throw Invalid("Invalid trigger timestamp.");
        var trend = command.TriggerEvent.FuturesItiSignal?.IntrinsicTimeTrend;
        if (trend is not (IntrinsicTimeTrendType.UpTrend or IntrinsicTimeTrendType.DownTrend)) throw Invalid("Invalid trigger direction.");
        var alignment = Age(at, triggerAt) >= p.TriggerMaximumAgeSeconds ? AssessmentTriggerAlignment.NotApplicable
            : decision.Direction == RegimeDirection.Neutral ? AssessmentTriggerAlignment.Neutral
            : (trend == IntrinsicTimeTrendType.UpTrend) == (decision.Direction == RegimeDirection.Up) ? AssessmentTriggerAlignment.Aligned : AssessmentTriggerAlignment.Conflicted;
        var triggerEvidence = new AssessmentEvidence(p.TargetHorizon, "ITI", "TriggerAlignment", null, "", triggerAt, Age(at,triggerAt), MarketSourceAvailability.Available, alignment.ToString());
        evidence.Add(triggerEvidence);
        if (alignment == AssessmentTriggerAlignment.NotApplicable) reasons.Add("MC.ASSESSMENT.TRIGGER.STALE");
        decimal? confidence = available ? Math.Round(decision.Confidence * fitness, 6, MidpointRounding.AwayFromZero) : null;
        foreach(var term in new (string Feature,decimal Value,string Unit)[]
        {
            ("RequiredFreshnessFactor",requiredFreshness,"ratio"),("RegimeFreshnessFactor",regimeFreshness,"ratio"),
            ("AppliedFreshnessFactor",fitness,"ratio"),("HealthySpreadThreshold",p.HealthySpreadTicks,"ticks"),
            ("DegradedSpreadThreshold",p.DegradedSpreadTicks,"ticks"),("HealthySizeThreshold",p.HealthyBestSize,"contracts"),
            ("DegradedSizeThreshold",p.DegradedBestSize,"contracts"),("MovementStressThreshold",p.MovementStressThreshold,"ATR ratio"),
            ("VolatilityStressThreshold",p.VolatilityChangeStressThreshold,"relative change")
        }) evidence.Add(new(p.TargetHorizon,"Calculation",term.Feature,term.Value,term.Unit,at,0,MarketSourceAvailability.Available,"Applied frozen policy; diagnostic evidence, not independent market authority"));
        var summary = available ? string.Create(CultureInfo.InvariantCulture, $"{p.InstrumentRoot} {p.TargetHorizon}: {condition}; liquidity {liquidity}; stress {stress}; session {s.SessionState}; events {s.EventContext}; confidence {confidence:0.000000}.")
            : $"{p.InstrumentRoot} {p.TargetHorizon}: Unavailable ({string.Join(", ", reasons.Distinct().Order(StringComparer.Ordinal))}).";
        var result = new MarketConditionAssessmentResult
        {
            ResultId = resultId, WorkflowId = command.WorkflowId, EntityId = command.WorkflowEntityId, CommandId = command.CommandId,
            InputWorkflowRevision = command.InputWorkflowRevision, MarketProfileId = p.MarketProfileId, InstrumentRoot = p.InstrumentRoot,
            ParameterSetId = p.ParameterSetId, ParameterSetVersion = p.Version, ParameterPayloadSha256 = command.ParameterPayloadSha256,
            RegimeResultId = regime.ResultId, RegimePayloadSha256 = command.RegimePayloadSha256, SnapshotId = s.SnapshotId,
            SnapshotSha256 = s.PayloadSha256, EvaluatedAtUtc = at, TargetHorizon = p.TargetHorizon, SummaryText = summary, CalendarEvidence = coverage,
            Assessment = new()
            {
                Horizon = p.TargetHorizon, Availability = available ? AssessmentAvailability.Available : AssessmentAvailability.Unavailable,
                RegimeResultId = regime.ResultId, RegimePayloadSha256 = command.RegimePayloadSha256, UpstreamContext = available ? decision : null,
                ConditionType = condition, LiquidityCondition = liquidity, StressState = stress, SessionState = s.SessionState, EventRiskState = s.EventContext,
                VolatilityBehavior = !available ? AssessmentVolatility.Unknown : stress == AssessmentStress.Elevated ? AssessmentVolatility.Shock : decision.VolatilityChange switch
                { VolatilityRegimeChange.Stable => AssessmentVolatility.Stable, VolatilityRegimeChange.Expanding => AssessmentVolatility.Expanding, VolatilityRegimeChange.Contracting => AssessmentVolatility.Contracting, _ => AssessmentVolatility.Unknown },
                TriggerAlignment = alignment, AssessmentConfidence = confidence, DataQuality = !available ? MarketConditionDataQuality.Unusable : reasons.Count > 0 ? MarketConditionDataQuality.Degraded : MarketConditionDataQuality.Healthy,
                EvaluatedAtUtc = at, ValidUntilUtc = available ? expiry : null, EvidenceItems = evidence.OrderBy(x => x.SourceId,StringComparer.Ordinal).ThenBy(x => x.Feature,StringComparer.Ordinal).ToArray(),
                ConflictingEvidenceItems = alignment == AssessmentTriggerAlignment.Conflicted ? [triggerEvidence] : [],
                LimitationReasons = reasons.Distinct().Order(StringComparer.Ordinal).ToArray(), InheritedRestrictions = decision.Restrictions.Distinct().Order().ToArray(), SummaryText = summary
            }
        };
        MarketConditionAssessmentContracts.ValidateResult(result);
        return result;
    }

    static decimal Age(DateTime now, DateTime at) => Math.Max(0m, (now.Ticks - at.Ticks) / (decimal)TimeSpan.TicksPerSecond);
    static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    static ArgumentException Invalid(string reason) => new(reason);
}
