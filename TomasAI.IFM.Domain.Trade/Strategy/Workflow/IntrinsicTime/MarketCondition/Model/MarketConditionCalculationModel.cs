using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;

public sealed record MarketConditionCalculationInput
{
    public Guid ResultId { get; init; }
    public long InputWorkflowRevision { get; init; }
    public IntrinsicTimeStrategyWorkflowView WorkflowView { get; init; } = new();
    public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    public RegimeDiscoveryResult RegimeResult { get; init; } = new();
    public MarketConditionParameterSet ParameterSet { get; init; } = new();
    public MarketConditionSnapshot Snapshot { get; init; } = new();
    public int OptionalMissingCategoryCount { get; init; }
    public int ConflictingEvidenceCount { get; init; }
    public MarketConditionEvidenceItem[] ConflictingEvidenceItems { get; init; } = [];
}

/// <summary>Evaluates all gates, primary market language, and schema-V2 advisory hints without reading external state.</summary>
public sealed class MarketConditionCalculationModel
{
    public MarketConditionResult Calculate(MarketConditionCalculationInput input)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = MarketConditionTelemetry.Start("market-condition.classify-and-score");
        ArgumentNullException.ThrowIfNull(input);
        var p = input.ParameterSet;
        var s = input.Snapshot;
        var signal = input.TriggerEvent.FuturesItiSignal
            ?? throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "The ITI trigger payload is missing.");
        ValidateIdentity(input);
        ValidateSources(s, p);

        if (!double.IsFinite(signal.BandLevel) || !double.IsFinite(signal.ReversalLevel))
            throw Invalid("The ITI trigger contains a non-finite numeric value.");

        var triggerDirection = signal.IntrinsicTimeTrend switch
        {
            IntrinsicTimeTrendType.UpTrend => MarketConditionDirection.Bullish,
            IntrinsicTimeTrendType.DownTrend => MarketConditionDirection.Bearish,
            _ => throw Invalid("The ITI trigger trend is not directional.")
        };
        var decisionDirection = input.RegimeResult.Decision.Direction;
        var direction = Direction(decisionDirection);
        var alignment = Alignment(triggerDirection, decisionDirection);
        if (alignment == MarketConditionUpstreamAlignment.Unknown)
            throw Invalid("The upstream Regime direction is unknown.");

        var evidence = new List<MarketConditionEvidenceItem>();
        var blockers = new List<MarketConditionBlockingReason>();
        AddDecisionEvidence(input.RegimeResult, evidence);
        GateWorkflow(input, blockers);
        if (alignment == MarketConditionUpstreamAlignment.Conflict)
            Add(blockers, MarketConditionEvidenceArea.Workflow, MarketConditionReasonCodes.RegimeTriggerConflict);
        GateData(s, p, blockers, evidence);
        GateSession(s, p, blockers);
        GateEventRisk(s, blockers);
        GateIntegrity(s, p, blockers);
        var futuresScore = GateFutures(s, p, blockers, evidence);
        var optionScore = GateOptions(s, p, blockers, evidence);
        GateOperations(s, p, blockers);

        var phase = Phase(input.RegimeResult.Decision.TrendPhase, signal, p.Classification);
        var volatility = Volatility(input.RegimeResult, s, p);
        var condition = Classify(input.RegimeResult, direction, alignment, volatility, blockers);
        var dataScore = DataQualityScore(s, p);
        decimal strength = 0m;
        decimal confidence = 0m;
        if (blockers.Count == 0)
        {
            var triggerQuality = TriggerQuality(signal);
            var decisionQuality = DecisionQuality(input.RegimeResult);
            var regimeAlignment = Clamp(0.70m * AlignmentScore(alignment) + 0.30m * decisionQuality);
            var entryTiming = EntryTiming(s.SessionState.ExchangeLocalTime, p.Session);
            strength = Math.Round(100m * Clamp(
                p.Scoring.RegimeAlignmentWeight * regimeAlignment +
                p.Scoring.TriggerQualityWeight * triggerQuality +
                p.Scoring.FuturesLiquidityWeight * futuresScore +
                p.Scoring.OptionLiquidityWeight * optionScore +
                p.Scoring.DataQualityWeight * dataScore +
                p.Scoring.EntryTimingWeight * entryTiming), 0, MidpointRounding.AwayFromZero);
            var penalties = Math.Min(p.Scoring.MaximumTotalPenalty,
                Math.Min(p.Scoring.OptionalMissingMaximumPenalty,
                    input.OptionalMissingCategoryCount * p.Scoring.OptionalMissingPenalty) +
                (input.RegimeResult.Decision.Restrictions.Contains(RegimeRestriction.LowConfidence)
                    ? p.Scoring.LowConfidencePenalty : 0m) +
                (condition == MarketConditionType.Transition ? p.Scoring.TransitionPenalty : 0m) +
                Math.Min(p.Scoring.ConflictingEvidenceMaximumPenalty,
                    Math.Max(input.ConflictingEvidenceCount, input.ConflictingEvidenceItems.Length) *
                    p.Scoring.ConflictingEvidencePenalty));
            confidence = Round(Clamp(0.40m * decisionQuality +
                0.20m * triggerQuality + 0.15m * dataScore + 0.125m * futuresScore +
                0.125m * optionScore - penalties));
            evidence.Add(ScoreEvidence("RegimeAlignment", regimeAlignment,
                p.Scoring.RegimeAlignmentWeight, "Regime", input.RegimeResult.ProducedAtUtc));
            evidence.Add(ScoreEvidence("TriggerQuality", triggerQuality,
                p.Scoring.TriggerQualityWeight, "ITI", input.TriggerEvent.CreatedOn));
            evidence.Add(ScoreEvidence("FuturesLiquidity", futuresScore,
                p.Scoring.FuturesLiquidityWeight, s.FuturesQuote.QuoteObservation.SourceId,
                s.FuturesQuote.QuoteObservation.SourceTimestampUtc));
            evidence.Add(ScoreEvidence("OptionLiquidity", optionScore,
                p.Scoring.OptionLiquidityWeight, s.OptionChainQuality.Observation.SourceId,
                s.OptionChainQuality.Observation.SourceTimestampUtc));
            evidence.Add(ScoreEvidence("DataQuality", dataScore,
                p.Scoring.DataQualityWeight, "Snapshot", s.MarketDataAsOfUtc));
            evidence.Add(ScoreEvidence("EntryTiming", entryTiming,
                p.Scoring.EntryTimingWeight, s.SessionState.Observation.SourceId,
                s.SessionState.Observation.SourceTimestampUtc));
            for (var index = 0; index < input.OptionalMissingCategoryCount; index++)
                evidence.Add(new MarketConditionEvidenceItem
                {
                    Area = MarketConditionEvidenceArea.Data,
                    FeatureCode = $"OptionalInput.{index + 1:D3}",
                    Availability = MarketSourceAvailability.Unavailable,
                    ReasonCode = MarketConditionReasonCodes.OptionalMissing
                });
        }

        if (blockers.Count == 0 && strength < p.Scoring.MinimumStrength)
            Add(blockers, MarketConditionEvidenceArea.Scoring, MarketConditionReasonCodes.Strength);
        if (blockers.Count == 0 && confidence < p.Scoring.MinimumConfidence)
            Add(blockers, MarketConditionEvidenceArea.Scoring, MarketConditionReasonCodes.Confidence);
        var tradeable = blockers.Count == 0 &&
            condition is not (MarketConditionType.Dislocated or MarketConditionType.NoOpportunity) &&
            phase is not (MarketConditionPhase.Exhausting or MarketConditionPhase.Undefined);
        if (!tradeable && blockers.Count == 0)
            Add(blockers, MarketConditionEvidenceArea.Classification, MarketConditionReasonCodes.Strength);
        if (!tradeable && condition is not MarketConditionType.Dislocated)
            condition = MarketConditionType.NoOpportunity;

        var reason = blockers.FirstOrDefault()?.ReasonCode ?? ConditionReason(condition);
        var liquidity = blockers.Any(x => x.Area is MarketConditionEvidenceArea.FuturesLiquidity or MarketConditionEvidenceArea.OptionLiquidity)
            ? MarketConditionLiquidityQuality.Unusable
            : futuresScore >= p.Classification.HealthyLiquidityScore && optionScore >= p.Classification.HealthyLiquidityScore
                ? MarketConditionLiquidityQuality.Healthy : MarketConditionLiquidityQuality.Degraded;
        var dataQuality = blockers.Any(x => x.Area == MarketConditionEvidenceArea.Data)
            ? MarketConditionDataQuality.Unusable
            : dataScore >= p.Classification.HealthyDataQualityScore
                ? MarketConditionDataQuality.Healthy : MarketConditionDataQuality.Degraded;
        var result = new MarketConditionResult
        {
            ResultId = input.ResultId,
            WorkflowId = input.WorkflowView.WorkflowId,
            EntityId = input.WorkflowView.EntityId,
            FundId = p.FundId,
            InstrumentRoot = p.InstrumentRoot,
            TargetHorizon = p.TargetHorizon,
            TriggerEventId = input.TriggerEvent.Id,
            InputWorkflowRevision = input.InputWorkflowRevision,
            StrategyParameterSetId = p.StrategyParameterSetId,
            StrategyParameterSetVersion = p.StrategyParameterSetVersion,
            MarketConditionParameterSetId = p.ParameterSetId,
            MarketConditionParameterSetVersion = p.Version,
            SnapshotId = s.SnapshotId,
            SnapshotSha256 = s.SnapshotSha256,
            EvaluatedAtUtc = s.EvaluationTimestampUtc,
            ValidUntilUtc = s.EvaluationTimestampUtc.AddSeconds(p.Execution.ResultLifetimeSeconds),
            MarketDataAsOfUtc = s.MarketDataAsOfUtc,
            Tradeability = tradeable ? MarketTradeability.Tradeable : MarketTradeability.NotTradeable,
            ConditionType = condition,
            Direction = direction,
            Phase = phase,
            Strength = strength,
            Confidence = confidence,
            VolatilityBehavior = volatility,
            LiquidityQuality = liquidity,
            DataQuality = dataQuality,
            UpstreamAlignment = alignment,
            EvidenceItems = evidence.OrderBy(x => x.Area).ThenBy(x => x.FeatureCode, StringComparer.Ordinal).ToArray(),
            ConflictingEvidenceItems = input.ConflictingEvidenceItems
                .OrderBy(x => x.Area).ThenBy(x => x.FeatureCode, StringComparer.Ordinal)
                .ThenBy(x => x.SourceTimestampUtc).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToArray(),
            BlockingReasons = blockers.ToArray(),
            PrimaryReasonCode = reason,
            Reasons = blockers.Count == 0 ? [reason] :
                blockers.Select(x => x.ReasonCode).Distinct(StringComparer.Ordinal).ToArray(),
            OutputHints = [BuildOutputHint(p.TargetHorizon,
                tradeable ? MarketTradeability.Tradeable : MarketTradeability.NotTradeable,
                condition, confidence, futuresScore, optionScore)]
        };
        var completed = result with { SummaryText = Summary(result, reason) };
        ValidateResult(completed, p);
        MarketConditionTelemetry.RecordResult(completed, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return completed;
    }

    internal static void ValidateResult(MarketConditionResult result, MarketConditionParameterSet parameters)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(parameters);
        var invalid = result.SchemaVersion != MarketConditionResult.CurrentSchemaVersion ||
                      result.ResultId == Guid.Empty || result.SnapshotId == Guid.Empty ||
                      result.SnapshotSha256.Length != 64 || !result.SnapshotSha256.All(Uri.IsHexDigit) ||
                      result.EvaluatedAtUtc == default || result.ValidUntilUtc <= result.EvaluatedAtUtc ||
                      result.MarketDataAsOfUtc == default || result.MarketDataAsOfUtc > result.EvaluatedAtUtc ||
                      result.Strength is < 0m or > 100m || result.Confidence is < 0m or > 1m ||
                      result.Tradeability == MarketTradeability.Undefined ||
                      result.OutputHints.Length != 1 ||
                      result.OutputHints.Any(x => x.TradeType == MarketConditionTradeType.Unknown ||
                          x.TimeFrame != result.TargetHorizon || x.Suitability == MarketConditionHintSuitability.Unknown ||
                          x.Confidence is < 0m or > 1m || string.IsNullOrWhiteSpace(x.ReasonCode) || !x.IsAdvisory) ||
                      string.IsNullOrWhiteSpace(result.PrimaryReasonCode) ||
                      string.IsNullOrWhiteSpace(result.SummaryText) || result.Reasons.Length == 0;
        if (result.Tradeability == MarketTradeability.Tradeable)
            invalid |= result.BlockingReasons.Length != 0 ||
                       result.ConditionType is MarketConditionType.Dislocated or MarketConditionType.NoOpportunity or
                           MarketConditionType.Undefined ||
                       result.Phase is MarketConditionPhase.Exhausting or MarketConditionPhase.Undefined ||
                       result.Strength < parameters.Scoring.MinimumStrength ||
                       result.Confidence < parameters.Scoring.MinimumConfidence;
        else
            invalid |= result.BlockingReasons.Length == 0;
        if (invalid)
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.InvariantViolation,
                MarketConditionReasonCodes.Invariant, "The completed Market Condition result violates a V1 invariant.");
    }

    static void ValidateIdentity(MarketConditionCalculationInput x)
    {
        if (x.ResultId == Guid.Empty || x.Snapshot.SnapshotId == Guid.Empty ||
            x.WorkflowView.Status != WorkflowStrategyMachineStatus.Started ||
            x.WorkflowView.CurrentStage != StrategyWorkflowStage.MarketCondition ||
            x.WorkflowView.WorkflowRevision != x.InputWorkflowRevision ||
            x.WorkflowView.WorkflowId != x.RegimeResult.WorkflowId ||
            x.WorkflowView.EntityId != x.RegimeResult.EntityId ||
            x.WorkflowView.WorkflowId != x.Snapshot.WorkflowId ||
            x.WorkflowView.EntityId != x.Snapshot.EntityId ||
            !x.RegimeResult.Decision.IsComplete ||
            x.ParameterSet.FundId != x.Snapshot.FundId ||
            x.ParameterSet.TargetHorizon != x.Snapshot.TargetHorizon ||
            x.ParameterSet.TargetHorizon != x.RegimeResult.TargetHorizon)
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.ContractInvalid,
                MarketConditionReasonCodes.ContractInvalid, "Market Condition identities are inconsistent.");
        if (!string.Equals(MarketConditionSnapshotHash.Compute(x.Snapshot), x.Snapshot.SnapshotSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new MarketConditionCalculationException(MarketConditionFailureCategory.RequiredInputInvalid,
                MarketConditionReasonCodes.RequiredInput, "Market Condition snapshot hash is invalid.");
    }

    static void ValidateSources(MarketConditionSnapshot s, MarketConditionParameterSet p)
    {
        if (s.EvaluationTimestampUtc == default || s.MarketDataAsOfUtc == default ||
            s.MarketDataAsOfUtc > s.EvaluationTimestampUtc.AddSeconds(p.Snapshot.FutureClockSkewSeconds))
            throw Invalid("Snapshot timestamps are invalid.");
        foreach (var o in Required(s))
            if (string.IsNullOrWhiteSpace(o.SourceId) || o.SourceTimestampUtc == default || o.SequenceId < 0 ||
                o.Validity != MarketSourceValidity.Valid || o.Availability == MarketSourceAvailability.Unknown ||
                o.AgeSeconds < 0 ||
                o.SourceTimestampUtc > s.EvaluationTimestampUtc.AddSeconds(p.Snapshot.FutureClockSkewSeconds) ||
                (o.ReceivedAtUtc != default &&
                 o.ReceivedAtUtc < o.SourceTimestampUtc.AddSeconds(-p.Snapshot.FutureClockSkewSeconds)))
                throw Invalid("Required source metadata is missing or invalid.");
        if (s.SessionState.Status == MarketSessionStatus.Unknown ||
            s.EventRiskState.Status == MarketEventRiskStatus.Unknown ||
            s.OperationalHealth.GroupBy(x => x.SourceId, StringComparer.Ordinal).Any(x => x.Count() != 1))
            throw Invalid("Required provider state is unknown or contradictory.");
        static MarketConditionCalculationException Invalid(string message) => new(
            MarketConditionFailureCategory.RequiredInputInvalid, MarketConditionReasonCodes.RequiredInput, message);
    }

    static IEnumerable<MarketSourceObservation> Required(MarketConditionSnapshot s)
        => new[] { s.FuturesQuote.QuoteObservation, s.FuturesQuote.TradeObservation,
            s.OptionChainQuality.Observation, s.SessionState.Observation, s.EventRiskState.Observation,
            s.VolatilityShockState.Observation }.Concat(s.OperationalHealth.Select(x => x.Observation));

    static void GateWorkflow(MarketConditionCalculationInput x, List<MarketConditionBlockingReason> b)
    {
        var s = x.Snapshot.WorkflowEligibility; var p = x.ParameterSet.WorkflowEligibility;
        if (!s.EntriesEnabled && p.RequireEntriesEnabled)
            Add(b, MarketConditionEvidenceArea.Workflow, MarketConditionReasonCodes.WorkflowIneligible);
        if (x.RegimeResult.Decision.Restrictions.Intersect(p.BlockingRegimeRestrictions).Any())
            Add(b, MarketConditionEvidenceArea.Workflow, MarketConditionReasonCodes.RegimeNoNewTrade);
        if ((x.Snapshot.EvaluationTimestampUtc - s.RegimeProducedAtUtc).TotalSeconds > p.MaximumRegimeAgeSeconds ||
            (x.Snapshot.EvaluationTimestampUtc - s.TriggerProducedAtUtc).TotalSeconds > p.MaximumTriggerAgeSeconds)
            Add(b, MarketConditionEvidenceArea.Data, MarketConditionReasonCodes.DataUnfit);
    }

    static void GateData(MarketConditionSnapshot s, MarketConditionParameterSet p,
        List<MarketConditionBlockingReason> b, List<MarketConditionEvidenceItem> e)
    {
        var limits = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [s.FuturesQuote.QuoteObservation.SourceId] = p.Snapshot.FuturesQuoteMaximumAgeSeconds,
            [s.FuturesQuote.TradeObservation.SourceId] = p.Snapshot.FuturesTradeMaximumAgeSeconds,
            [s.OptionChainQuality.Observation.SourceId] = p.Snapshot.OptionChainMaximumAgeSeconds,
            [s.SessionState.Observation.SourceId] = p.Snapshot.SessionMaximumAgeSeconds,
            [s.EventRiskState.Observation.SourceId] = p.Snapshot.EventRiskMaximumAgeSeconds,
            [s.VolatilityShockState.Observation.SourceId] = p.Snapshot.VolatilityMaximumAgeSeconds
        };
        foreach (var o in Required(s))
        {
            var max = limits.GetValueOrDefault(o.SourceId, p.Snapshot.HealthMaximumAgeSeconds);
            var stale = o.AgeSeconds > max;
            var unavailable = o.Availability == MarketSourceAvailability.Unavailable;
            if (stale || unavailable)
                Add(b, MarketConditionEvidenceArea.Data, MarketConditionReasonCodes.DataUnfit, o.SourceId);
            e.Add(Evidence(MarketConditionEvidenceArea.Data, o.SourceId, o.AgeSeconds, "seconds",
                Clamp(1m - o.AgeSeconds / max), stale ? MarketConditionReasonCodes.DataStale : MarketConditionReasonCodes.DataFit, o));
        }
    }

    static void GateSession(MarketConditionSnapshot s, MarketConditionParameterSet p, List<MarketConditionBlockingReason> b)
    {
        if ((p.Session.RequireOpenExchangeState && s.SessionState.Status != MarketSessionStatus.Open) ||
            !s.SessionState.IsEntryWindow || !p.Session.EligibleWeekdays.Contains(s.SessionState.ExchangeLocalWeekday))
            Add(b, MarketConditionEvidenceArea.Session, MarketConditionReasonCodes.Session);
    }
    static void GateEventRisk(MarketConditionSnapshot s, List<MarketConditionBlockingReason> b)
    { if (s.EventRiskState.Status == MarketEventRiskStatus.Blocked) Add(b, MarketConditionEvidenceArea.EventRisk, MarketConditionReasonCodes.EventRisk); }
    static void GateIntegrity(MarketConditionSnapshot s, MarketConditionParameterSet p, List<MarketConditionBlockingReason> b)
    {
        var q = s.FuturesQuote;
        if ((p.MarketIntegrity.RequirePositiveTwoSidedQuote && (q.BidPrice <= 0 || q.AskPrice <= 0)) ||
            (!p.MarketIntegrity.PermitCrossedMarket && q.AskPrice < q.BidPrice) ||
            Math.Abs(q.OneMinuteMoveAtr) > p.MarketIntegrity.MaximumOneMinuteMoveAtr ||
            s.VolatilityShockState.FiveMinuteRelativeIncrease > p.MarketIntegrity.MaximumFiveMinuteVolatilityIncrease)
            Add(b, MarketConditionEvidenceArea.MarketIntegrity, MarketConditionReasonCodes.MarketDislocated);
    }
    static decimal GateFutures(MarketConditionSnapshot s, MarketConditionParameterSet p,
        List<MarketConditionBlockingReason> b, List<MarketConditionEvidenceItem> e)
    {
        var q = s.FuturesQuote; var c = p.FuturesLiquidity;
        if (c.TickSize <= 0 || (q.AskPrice - q.BidPrice) % c.TickSize != 0) throw Invalid("Futures quote is not tick aligned.");
        var spread = (q.AskPrice - q.BidPrice) / c.TickSize;
        if (spread > c.MaximumTradeableSpreadTicks || q.BidSize < c.MinimumBidSize || q.AskSize < c.MinimumAskSize)
            Add(b, MarketConditionEvidenceArea.FuturesLiquidity, MarketConditionReasonCodes.FuturesLiquidity);
        var spreadScore = Clamp((c.MaximumTradeableSpreadTicks - spread) / (c.MaximumTradeableSpreadTicks - 1m));
        var depth = Clamp(Math.Min(q.BidSize, q.AskSize) / c.HealthyBestSideSize);
        var score = Round(0.60m * spreadScore + 0.40m * depth);
        e.Add(Evidence(MarketConditionEvidenceArea.FuturesLiquidity, "SpreadTicks", spread, "ticks", score,
            MarketConditionReasonCodes.DataFit, q.QuoteObservation));
        return score;
    }
    static decimal GateOptions(MarketConditionSnapshot s, MarketConditionParameterSet p,
        List<MarketConditionBlockingReason> b, List<MarketConditionEvidenceItem> e)
    {
        var q = s.OptionChainQuality; var c = p.OptionLiquidity;
        if (q.CandidateContractCount < c.MinimumCandidateContracts || q.EligibleExpirationCount < c.MinimumEligibleExpirations ||
            q.ValidQuoteCoverage < c.MinimumValidQuoteCoverage || q.MedianRelativeSpread > c.MaximumMedianRelativeSpread ||
            q.P90RelativeSpread > c.MaximumP90RelativeSpread || q.MedianBidSize < c.MinimumMedianBidSize ||
            q.MedianAskSize < c.MinimumMedianAskSize || q.UnderlyingMismatch > c.MaximumUnderlyingMismatch ||
            (c.RequireCallsAndPuts && (!q.HasCalls || !q.HasPuts)))
            Add(b, MarketConditionEvidenceArea.OptionLiquidity, MarketConditionReasonCodes.OptionLiquidity);
        var score = Round(0.40m * Clamp(q.ValidQuoteCoverage / c.HealthyValidQuoteCoverage) +
            0.35m * Clamp(1m - q.MedianRelativeSpread / c.MaximumMedianRelativeSpread) +
            0.15m * Clamp(Math.Min(q.MedianBidSize, q.MedianAskSize)) +
            0.10m * Clamp((decimal)q.EligibleExpirationCount / c.MinimumEligibleExpirations));
        e.Add(Evidence(MarketConditionEvidenceArea.OptionLiquidity, "QuoteCoverage", q.ValidQuoteCoverage,
            "ratio", score, MarketConditionReasonCodes.DataFit, q.Observation));
        return score;
    }
    static void GateOperations(MarketConditionSnapshot s, MarketConditionParameterSet p, List<MarketConditionBlockingReason> b)
    {
        foreach (var required in p.OperationalReadiness.RequiredHealthSources)
        {
            var item = s.OperationalHealth.SingleOrDefault(x => x.SourceId == required)
                ?? throw Invalid($"Required health source {required} is missing.");
            if (item.Status == MarketOperationalStatus.Unavailable ||
                (p.OperationalReadiness.TreatReportedDegradedAsBlocked && item.Status == MarketOperationalStatus.Degraded))
                Add(b, MarketConditionEvidenceArea.Operations, MarketConditionReasonCodes.Operations, required);
            if (item.Status == MarketOperationalStatus.Unknown) throw Invalid($"Health source {required} is unknown.");
        }
    }

    static MarketConditionPhase Phase(TrendRegimePhase decisionPhase,
        Domain.MarketData.Analytics.Shared.ViewModels.FuturesItiSignalV2ReadModel s,
        MarketConditionClassificationConfiguration p) => decisionPhase switch
    {
        TrendRegimePhase.RangeBound => MarketConditionPhase.Confirmed,
        TrendRegimePhase.Emerging => MarketConditionPhase.Initiating,
        TrendRegimePhase.Established => MarketConditionPhase.Continuing,
        TrendRegimePhase.Exhausting => MarketConditionPhase.Exhausting,
        TrendRegimePhase.Reversing => MarketConditionPhase.Reversing,
        _ => TriggerPhase(s, p)
    };

    static MarketConditionPhase TriggerPhase(
        Domain.MarketData.Analytics.Shared.ViewModels.FuturesItiSignalV2ReadModel s,
        MarketConditionClassificationConfiguration p) => s.IntrinsicTimeMode switch
    {
        IntrinsicTimeModeType.TrendDirectionChanged => MarketConditionPhase.Initiating,
        IntrinsicTimeModeType.TrendReversalChanged => MarketConditionPhase.Reversing,
        _ when (decimal)s.ReversalLevel >= p.ExhaustingReversalLevel => MarketConditionPhase.Exhausting,
        _ when (decimal)s.ReversalLevel >= p.WeakeningReversalLevel => MarketConditionPhase.Weakening,
        IntrinsicTimeModeType.TrendExtremeChanged => MarketConditionPhase.Continuing,
        _ when Math.Abs((decimal)s.BandLevel) >= p.ConfirmedBandLevel => MarketConditionPhase.Confirmed,
        IntrinsicTimeModeType.Trending or IntrinsicTimeModeType.PredictedIntervalChanged => MarketConditionPhase.Confirmed,
        _ => MarketConditionPhase.Undefined
    };

    static MarketConditionVolatilityBehavior Volatility(RegimeDiscoveryResult r, MarketConditionSnapshot s,
        MarketConditionParameterSet p)
        => s.VolatilityShockState.FiveMinuteRelativeIncrease > p.MarketIntegrity.MaximumFiveMinuteVolatilityIncrease ||
           Math.Abs(s.FuturesQuote.OneMinuteMoveAtr) > p.MarketIntegrity.MaximumOneMinuteMoveAtr
            ? MarketConditionVolatilityBehavior.Shock
            : EffectiveVolatilityChange(r) switch
            {
                VolatilityRegimeChange.Expanding => MarketConditionVolatilityBehavior.Expanding,
                VolatilityRegimeChange.Contracting => MarketConditionVolatilityBehavior.Contracting,
                _ => MarketConditionVolatilityBehavior.Stable
            };

    static MarketConditionType Classify(RegimeDiscoveryResult r, MarketConditionDirection d,
        MarketConditionUpstreamAlignment a, MarketConditionVolatilityBehavior v, List<MarketConditionBlockingReason> b)
    {
        if (b.Any(x => x.Area == MarketConditionEvidenceArea.MarketIntegrity)) return MarketConditionType.Dislocated;
        if (a == MarketConditionUpstreamAlignment.Conflict) return MarketConditionType.NoOpportunity;
        var structure = EffectiveStructure(r);
        if (r.Decision.Restrictions.Contains(RegimeRestriction.Transition) ||
            structure == MarketStructureClassification.Transitioning ||
            r.Decision.TrendPhase is TrendRegimePhase.Exhausting or TrendRegimePhase.Reversing)
            return MarketConditionType.Transition;
        if (v == MarketConditionVolatilityBehavior.Expanding && !r.Decision.Restrictions.Contains(RegimeRestriction.NoNewTrade))
            return MarketConditionType.VolatilityExpansion;
        if (v == MarketConditionVolatilityBehavior.Contracting &&
            structure is MarketStructureClassification.Ranging or MarketStructureClassification.Compressing &&
            r.Decision.Direction == RegimeDirection.Neutral) return MarketConditionType.VolatilityContraction;
        if (structure is MarketStructureClassification.BreakingOut || r.Decision.Breakout != MarketBreakoutState.None)
            return MarketConditionType.Directional;
        if (structure == MarketStructureClassification.Ranging && r.Decision.Direction == RegimeDirection.Neutral)
            return MarketConditionType.RangeBound;
        if (r.Decision.Direction is RegimeDirection.Up or RegimeDirection.Down && a == MarketConditionUpstreamAlignment.Aligned)
            return MarketConditionType.Directional;
        return MarketConditionType.NoOpportunity;
    }

    static MarketConditionUpstreamAlignment Alignment(MarketConditionDirection d, RegimeDirection r) => r switch
    {
        RegimeDirection.Neutral => MarketConditionUpstreamAlignment.Neutral,
        RegimeDirection.Up when d == MarketConditionDirection.Bullish => MarketConditionUpstreamAlignment.Aligned,
        RegimeDirection.Down when d == MarketConditionDirection.Bearish => MarketConditionUpstreamAlignment.Aligned,
        RegimeDirection.Up or RegimeDirection.Down => MarketConditionUpstreamAlignment.Conflict,
        _ => MarketConditionUpstreamAlignment.Unknown
    };
    static MarketConditionDirection Direction(RegimeDirection value) => value switch
    {
        RegimeDirection.Up => MarketConditionDirection.Bullish,
        RegimeDirection.Down => MarketConditionDirection.Bearish,
        RegimeDirection.Neutral => MarketConditionDirection.Neutral,
        _ => MarketConditionDirection.Undefined
    };
    static decimal AlignmentScore(MarketConditionUpstreamAlignment a) => a switch
        { MarketConditionUpstreamAlignment.Aligned => 1m, MarketConditionUpstreamAlignment.Neutral => 0.5m, _ => 0m };
    static VolatilityRegimeChange EffectiveVolatilityChange(RegimeDiscoveryResult result)
        => result.Decision.VolatilityChange != VolatilityRegimeChange.Unknown
            ? result.Decision.VolatilityChange
            : result.Volatility.Change;
    static MarketStructureClassification EffectiveStructure(RegimeDiscoveryResult result)
        => result.Decision.StructureClassification != MarketStructureClassification.Unknown
            ? result.Decision.StructureClassification
            : result.MarketStructure.Classification;
    static decimal DecisionQuality(RegimeDiscoveryResult result)
    {
        var decision = result.Decision;
        var confidence = decision.Confidence > 0m ? decision.Confidence : result.OverallConfidence;
        var directional = decision.DirectionalScore != 0m ? Math.Abs(decision.DirectionalScore) : confidence;
        var conviction = decision.RiskAdjustedConviction > 0m ? decision.RiskAdjustedConviction : directional;
        var agreement = decision.TrendTimeFrameAgreement > 0m
            ? decision.TrendTimeFrameAgreement
            : result.Trend.TimeFrameAgreement > 0m ? result.Trend.TimeFrameAgreement : confidence;
        var strength = decision.TrendStrength switch
        {
            TrendRegimeStrength.Weak => 0.25m,
            TrendRegimeStrength.Moderate => 0.50m,
            TrendRegimeStrength.Strong => 0.75m,
            TrendRegimeStrength.Extreme => 1m,
            _ => confidence
        };
        return Round(Clamp(0.40m * confidence + 0.20m * conviction + 0.15m * directional +
                           0.15m * agreement + 0.10m * strength));
    }
    static void AddDecisionEvidence(RegimeDiscoveryResult result, List<MarketConditionEvidenceItem> evidence)
    {
        var decision = result.Decision;
        Add("RD.Direction", (decimal)decision.Direction, decision.Direction.ToString());
        Add("RD.DirectionalScore", decision.DirectionalScore, string.Empty);
        Add("RD.RiskAdjustedConviction", decision.RiskAdjustedConviction, string.Empty);
        Add("RD.Confidence", decision.Confidence > 0m ? decision.Confidence : result.OverallConfidence,
            decision.ConfidenceBand.ToString());
        Add("RD.Quality", (decimal)decision.Quality, decision.Quality.ToString());
        Add("RD.TrendPhase", (decimal)decision.TrendPhase, decision.TrendPhase.ToString());
        Add("RD.TrendStrength", (decimal)decision.TrendStrength, decision.TrendStrength.ToString());
        Add("RD.TrendTimeFrameAgreement", decision.TrendTimeFrameAgreement, string.Empty);
        Add("RD.VolatilityLevel", (decimal)decision.VolatilityLevel, decision.VolatilityLevel.ToString());
        Add("RD.VolatilityChange", (decimal)EffectiveVolatilityChange(result),
            EffectiveVolatilityChange(result).ToString());
        Add("RD.TermStructure", (decimal)decision.TermStructure, decision.TermStructure.ToString());
        Add("RD.Structure", (decimal)EffectiveStructure(result), EffectiveStructure(result).ToString());
        Add("RD.Breakout", (decimal)decision.Breakout, decision.Breakout.ToString());
        Add("RD.RestrictionCount", decision.Restrictions.Length,
            string.Join(',', decision.Restrictions.Select(x => x.ToString())));
        Add("RD.ReasonCount", decision.Reasons.Length,
            string.Join(',', decision.Reasons.Select(x => x.Code)));
        return;

        void Add(string code, decimal value, string text) => evidence.Add(new MarketConditionEvidenceItem
        {
            Area = MarketConditionEvidenceArea.Classification,
            FeatureCode = code,
            ObservedValue = value,
            ObservedText = text,
            NormalizedValue = value is >= 0m and <= 1m ? value : 0m,
            SourceId = "RegimeDiscoveryDecision",
            SourceTimestampUtc = result.ProducedAtUtc,
            Availability = MarketSourceAvailability.Available,
            Freshness = MarketFreshnessState.Fresh,
            ReasonCode = MarketConditionReasonCodes.DataFit
        });
    }
    static MarketConditionOutputHint BuildOutputHint(TimeFrameType horizon, MarketTradeability tradeability,
        MarketConditionType condition, decimal confidence, decimal futuresScore, decimal optionScore)
    {
        var (tradeType, reason, liquidityScore, preferred) = horizon switch
        {
            TimeFrameType.Daily => (MarketConditionTradeType.Futures,
                MarketConditionReasonCodes.HintFuturesDaily, futuresScore,
                condition is MarketConditionType.Directional or MarketConditionType.VolatilityExpansion),
            TimeFrameType.Weekly => (MarketConditionTradeType.VerticalSpread,
                MarketConditionReasonCodes.HintVerticalSpreadWeekly, optionScore,
                condition is MarketConditionType.Directional or MarketConditionType.VolatilityExpansion),
            TimeFrameType.Monthly => (MarketConditionTradeType.IronCondor,
                MarketConditionReasonCodes.HintIronCondorMonthly, optionScore,
                condition is MarketConditionType.RangeBound or MarketConditionType.VolatilityContraction),
            _ => (MarketConditionTradeType.Unknown, string.Empty, 0m, false)
        };
        var suitability = tradeability != MarketTradeability.Tradeable
            ? MarketConditionHintSuitability.Avoid
            : preferred ? MarketConditionHintSuitability.Preferred : MarketConditionHintSuitability.Eligible;
        var hintConfidence = suitability switch
        {
            MarketConditionHintSuitability.Preferred => Math.Min(confidence, liquidityScore),
            MarketConditionHintSuitability.Eligible => 0.75m * Math.Min(confidence, liquidityScore),
            _ => 0m
        };
        return new MarketConditionOutputHint
        {
            TradeType = tradeType,
            TimeFrame = horizon,
            Suitability = suitability,
            Confidence = Round(hintConfidence),
            ReasonCode = reason
        };
    }
    static decimal TriggerQuality(Domain.MarketData.Analytics.Shared.ViewModels.FuturesItiSignalV2ReadModel s)
    {
        var factor = s.IntrinsicTimeMode switch
        {
            IntrinsicTimeModeType.TrendDirectionChanged => 1m,
            IntrinsicTimeModeType.TrendExtremeChanged => 0.85m,
            IntrinsicTimeModeType.Trending => 0.75m,
            IntrinsicTimeModeType.PredictedIntervalChanged => 0.70m,
            IntrinsicTimeModeType.TrendReversalChanged => 0.60m,
            _ => 0.40m
        };
        return Round(Clamp(0.50m * factor + 0.30m * Clamp(Math.Abs((decimal)s.BandLevel)) +
            0.20m * Clamp(1m - (decimal)s.ReversalLevel)));
    }
    static decimal DataQualityScore(MarketConditionSnapshot s, MarketConditionParameterSet p)
    {
        var required = Required(s).ToArray();
        if (required.Length == 0) return 0m;
        return Round(required.Average(x => Clamp(1m - x.AgeSeconds / MaximumAge(x.SourceId, s, p))));
    }
    static int MaximumAge(string id, MarketConditionSnapshot s, MarketConditionParameterSet p)
    {
        if (id == s.FuturesQuote.QuoteObservation.SourceId) return p.Snapshot.FuturesQuoteMaximumAgeSeconds;
        if (id == s.FuturesQuote.TradeObservation.SourceId) return p.Snapshot.FuturesTradeMaximumAgeSeconds;
        if (id == s.OptionChainQuality.Observation.SourceId) return p.Snapshot.OptionChainMaximumAgeSeconds;
        if (id == s.SessionState.Observation.SourceId) return p.Snapshot.SessionMaximumAgeSeconds;
        if (id == s.EventRiskState.Observation.SourceId) return p.Snapshot.EventRiskMaximumAgeSeconds;
        if (id == s.VolatilityShockState.Observation.SourceId) return p.Snapshot.VolatilityMaximumAgeSeconds;
        return p.Snapshot.HealthMaximumAgeSeconds;
    }
    static decimal EntryTiming(TimeSpan t, MarketConditionSessionConfiguration p)
    {
        var total = (decimal)(p.EntryWindowEnd - p.EntryWindowStart).TotalSeconds;
        if (total <= 0) return 0m;
        var midpoint = p.EntryWindowStart + TimeSpan.FromSeconds((double)(total / 2m));
        var half = total / 2m;
        return Round(1m - 0.2m * Math.Abs((decimal)(t - midpoint).TotalSeconds) / half);
    }
    static MarketConditionEvidenceItem Evidence(MarketConditionEvidenceArea area, string feature, decimal value,
        string unit, decimal normalized, string reason, MarketSourceObservation o) => new()
        { Area = area, FeatureCode = feature, ObservedValue = value, Unit = unit, NormalizedValue = normalized,
          SourceId = o.SourceId, SourceTimestampUtc = o.SourceTimestampUtc, SequenceId = o.SequenceId,
          Availability = o.Availability, Freshness = reason == MarketConditionReasonCodes.DataStale
              ? MarketFreshnessState.Stale : MarketFreshnessState.Fresh, ReasonCode = reason };
    static MarketConditionEvidenceItem ScoreEvidence(string feature, decimal normalized, decimal weight,
        string source, DateTime timestamp) => new()
    {
        Area = MarketConditionEvidenceArea.Scoring,
        FeatureCode = feature,
        NormalizedValue = normalized,
        WeightedContribution = Round(normalized * weight),
        SourceId = source,
        SourceTimestampUtc = timestamp,
        Availability = MarketSourceAvailability.Available,
        Freshness = MarketFreshnessState.Fresh,
        ReasonCode = MarketConditionReasonCodes.DataFit
    };
    static void Add(List<MarketConditionBlockingReason> b, MarketConditionEvidenceArea a, string reason, string source = "")
    { if (!b.Any(x => x.ReasonCode == reason && x.SourceId == source)) b.Add(new() { Area = a, ReasonCode = reason, SourceId = source }); }
    static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);
    static decimal Round(decimal value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
    static string ConditionReason(MarketConditionType c) => c switch
    {
        MarketConditionType.Directional => MarketConditionReasonCodes.Directional,
        MarketConditionType.RangeBound => MarketConditionReasonCodes.RangeBound,
        MarketConditionType.Transition => MarketConditionReasonCodes.Transition,
        MarketConditionType.VolatilityExpansion => MarketConditionReasonCodes.VolatilityExpansion,
        MarketConditionType.VolatilityContraction => MarketConditionReasonCodes.VolatilityContraction,
        _ => MarketConditionReasonCodes.Strength
    };
    static string Summary(MarketConditionResult r, string reason)
    {
        var hint = r.OutputHints.Single();
        return $"{r.TargetHorizon} {r.InstrumentRoot} condition is {r.Tradeability}: {r.Direction} {r.ConditionType}, " +
               $"{r.Phase} phase, strength {r.Strength:0}, confidence {r.Confidence:0.00}. {reason}. " +
               $"Advisory hint: {hint.TradeType}/{hint.TimeFrame} {hint.Suitability}.";
    }
    static MarketConditionCalculationException Invalid(string message) => new(
        MarketConditionFailureCategory.RequiredInputInvalid, MarketConditionReasonCodes.RequiredInput, message);
}

public sealed class MarketConditionCalculationException(
    MarketConditionFailureCategory category, string reasonCode, string message) : Exception(message)
{
    public MarketConditionFailureCategory Category { get; } = category;
    public string ReasonCode { get; } = reasonCode;
}
