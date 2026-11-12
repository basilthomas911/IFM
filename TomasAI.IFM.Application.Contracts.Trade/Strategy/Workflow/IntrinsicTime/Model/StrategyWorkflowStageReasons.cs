using System.Text.RegularExpressions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Records operator-facing reasons from committed workflow transition evidence.</summary>
public static partial class StrategyWorkflowStageReasons
{
    /// <summary>Attaches reasons to the five stage states before the workflow snapshot is written.</summary>
    public static IntrinsicTimeStrategyWorkflowView Record(IntrinsicTimeStrategyWorkflowView view) => view with
    {
        RegimeDiscovery = Record(view, StrategyWorkflowStage.RegimeDiscovery, view.RegimeDiscovery),
        MarketCondition = Record(view, StrategyWorkflowStage.MarketCondition, view.MarketCondition),
        TradeSelection = Record(view, StrategyWorkflowStage.TradeSelection, view.TradeSelection),
        OrderComposition = Record(view, StrategyWorkflowStage.OrderComposition, view.OrderComposition),
        RiskManagement = Record(view, StrategyWorkflowStage.RiskManagement, view.RiskManagement)
    };

    static StrategyWorkflowStageState Record(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage,
        StrategyWorkflowStageState state) => state with { ReasonSentence = Describe(view, stage, state) };

    /// <summary>Derives a reason from historical evidence when a snapshot predates recorded sentences.</summary>
    public static string Describe(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage, StrategyWorkflowStageState state)
    {
        var identity = view.EntityId.ItiSignalEntityId;
        var market = string.IsNullOrWhiteSpace(identity.ContractId)
            ? "this market"
            : $"{identity.TimePeriod} {identity.ContractId}";
        var name = Words(stage);
        var sentence = state.ProcessingStatus switch
        {
            StrategyActorProcessingStatus.NotStarted => Waiting(view, stage, name),
            StrategyActorProcessingStatus.Processing => Processing(view, stage, market),
            StrategyActorProcessingStatus.Completed when state.ContinuationDecision == StrategyWorkflowContinuationDecision.Proceed =>
                Completed(view, stage, market),
            StrategyActorProcessingStatus.Completed when stage == StrategyWorkflowStage.RiskManagement
                && view.Outcome == StrategyWorkflowOutcome.Completed => Completed(view, stage, market),
            StrategyActorProcessingStatus.Completed when state.ContinuationDecision == StrategyWorkflowContinuationDecision.Stop =>
                Stopped(view, stage, state, market),
            StrategyActorProcessingStatus.Completed => $"{name} is awaiting the continuation decision for {market}",
            StrategyActorProcessingStatus.TimedOut => $"{name} did not finish for {market} before its deadline",
            StrategyActorProcessingStatus.Cancelled => $"{name} was interrupted when the {market} workflow was cancelled",
            _ => $"{name} failed for {market}"
        };
        if (state.ProcessingStatus is StrategyActorProcessingStatus.Failed or StrategyActorProcessingStatus.TimedOut)
        {
            var cause = Cause(state.Failure?.ErrorData) ?? Cause(state.Failure?.ErrorMessage);
            if (cause is not null)
                sentence = $"{sentence} because {cause}";
            else if (state.ProcessingStatus == StrategyActorProcessingStatus.Failed && state.Failure is { } failure
                && Prose(failure.ErrorMessage) is { } message)
                sentence = $"{name} failed for {market}: {message}";
            else if (view.CurrentStage == stage && view.Outcome == StrategyWorkflowOutcome.InvalidResult)
                sentence = $"{name} returned a result that did not match the accepted request for {market}";
        }
        return ShortSentence(sentence);
    }

    static string Waiting(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage, string name)
    {
        if (view.Status is WorkflowStrategyMachineStatus.Completed or WorkflowStrategyMachineStatus.Failed
            or WorkflowStrategyMachineStatus.TimedOut or WorkflowStrategyMachineStatus.Cancelled)
        {
            var endedAt = view.CurrentStage != StrategyWorkflowStage.None && view.CurrentStage < stage
                ? $" at {Words(view.CurrentStage)}" : "";
            var end = view.Outcome == StrategyWorkflowOutcome.NoTrade ? "ended with no trade" : view.Status switch
            {
                WorkflowStrategyMachineStatus.TimedOut => "timed out",
                WorkflowStrategyMachineStatus.Cancelled => "was cancelled",
                _ => "ended"
            };
            return $"{name} did not start because the workflow {end}{endedAt}";
        }
        var prior = stage switch
        {
            StrategyWorkflowStage.MarketCondition => "Regime Discovery",
            StrategyWorkflowStage.TradeSelection => "Market Condition",
            StrategyWorkflowStage.OrderComposition => "Trade Selection",
            StrategyWorkflowStage.RiskManagement => "Order Composition",
            _ => null
        };
        return prior is null ? $"{name} is waiting for the workflow to start" : $"{name} is waiting for {prior} to finish";
    }

    static string Processing(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage, string market) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery => $"Regime Discovery is classifying trend and volatility for {market}",
        StrategyWorkflowStage.MarketCondition => $"Market Condition is checking liquidity and event risk for {market}",
        StrategyWorkflowStage.TradeSelection => $"Trade Selection is checking which strategies meet the entry rules for {market}",
        StrategyWorkflowStage.OrderComposition => $"Order Composition is qualifying contracts and prices for {Strategy(view, market)}",
        StrategyWorkflowStage.RiskManagement when view.FinancialHandoff is { Phase: RiskFinancialHandoffPhase.ReservePending } =>
            $"Risk Management is waiting for capacity to be reserved for {Strategy(view, market)}",
        StrategyWorkflowStage.RiskManagement when view.FinancialHandoff is { Phase: RiskFinancialHandoffPhase.FundPending } =>
            $"Risk Management is waiting for the Fund to authorize {Strategy(view, market)}",
        StrategyWorkflowStage.RiskManagement when view.FinancialHandoff is { Phase: RiskFinancialHandoffPhase.ConsumePending } =>
            $"Risk Management is waiting for Portfolio to commit the reserved capacity for {Strategy(view, market)}",
        _ => $"Risk Management is checking the Fund's capacity for {Strategy(view, market)}"
    };

    static string Completed(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage, string market) => stage switch
    {
        StrategyWorkflowStage.RegimeDiscovery when view.RegimeDiscovery.Result?.RegimeResult is { } regime =>
            $"Regime Discovery found {Direction(regime.Decision.Direction)} and {Words(regime.Volatility.Level).ToLowerInvariant()} volatility in {market}",
        StrategyWorkflowStage.MarketCondition when view.MarketCondition.Result?.AssessmentResult?.Assessment.ConditionType is { } condition =>
            $"Market Condition classified {market} as {Words(condition).ToLowerInvariant()} and allowed Trade Selection",
        StrategyWorkflowStage.TradeSelection => $"Trade Selection chose {Strategy(view, market)} under the current entry rules",
        StrategyWorkflowStage.OrderComposition when view.OrderComposition.Result?.CompositionResult?.Candidate is { } order =>
            FormattableString.Invariant($"Order Composition selected {order.Legs.Length} eligible legs for {Strategy(view, market)}"),
        StrategyWorkflowStage.RiskManagement when view.PortfolioRiskDecision is { } decision =>
            FormattableString.Invariant($"Portfolio authorized {decision.TradeOrders.Length} {(decision.TradeOrders.Length == 1 ? "order" : "orders")} for {Strategy(view, market)}"),
        StrategyWorkflowStage.RiskManagement when view.RiskManagement.Result?.RiskResult is { StrategyUnits: > 0 } risk =>
            FormattableString.Invariant($"Risk Management approved {risk.StrategyUnits} {(risk.StrategyUnits == 1 ? "unit" : "units")} of {Strategy(view, market)}"),
        _ => $"{Words(stage)} accepted the evidence for {market}"
    };

    static string Stopped(IntrinsicTimeStrategyWorkflowView view, StrategyWorkflowStage stage, StrategyWorkflowStageState state, string market)
    {
        var codes = state.ContinuationReasonCodes;
        var cause = ResultCause(stage, state.Result)
            ?? FirstCause(codes)
            ?? Cause(view.StopReasonCode)
            ?? Cause(state.Failure?.ErrorData)
            ?? Cause(state.Failure?.ErrorMessage);
        if (codes.Contains("MC.ASSESSMENT.INHERITED_NO_NEW_TRADE", StringComparer.Ordinal)
            || codes.Contains("TS.REGIME.RESTRICTION", StringComparer.Ordinal))
        {
            var regime = view.RegimeDiscovery.Result?.RegimeResult;
            cause = regime is null ? null : VolatilityRestriction(view, regime.Volatility);
            cause ??= "Regime Discovery restricted new entries";
        }
        if (stage == StrategyWorkflowStage.TradeSelection
            && cause is "the market condition did not match the entry rules" or "no configured strategy matched the market conditions"
            && view.MarketCondition.Result?.AssessmentResult?.Assessment.ConditionType is { } condition)
            cause = $"the {Words(condition).ToLowerInvariant()} market did not match the entry rules";
        var subject = stage is StrategyWorkflowStage.OrderComposition or StrategyWorkflowStage.RiskManagement
            ? Strategy(view, market) : market;
        if (cause is not null)
            return $"{Words(stage)} stopped {subject} because {cause}";
        var detail = codes.Select(Prose).FirstOrDefault(static reason => reason is not null)
            ?? Prose(state.Failure?.ErrorMessage);
        return detail is null
            ? $"{Words(stage)} returned no eligible trade for {subject}"
            : $"{Words(stage)} stopped {subject}: {detail}";
    }

    static string? ResultCause(StrategyWorkflowStage stage, StrategyStageResultEnvelope? result) => stage switch
    {
        StrategyWorkflowStage.MarketCondition when result?.AssessmentResult is { } assessment =>
            FirstCause(assessment.Assessment.LimitationReasons),
        StrategyWorkflowStage.TradeSelection when result?.SelectionResult is { PrimaryReasonCode: "TS.NO_COMPATIBLE_CANDIDATE" } selection =>
            CommonSelectionCause(selection.CandidateDecisions.SelectMany(decision => decision.ReasonCodes)),
        StrategyWorkflowStage.OrderComposition when result?.CompositionResult is { } composition => FirstCause(composition.Reasons),
        StrategyWorkflowStage.RiskManagement when result?.RiskResult is { } risk => FirstCause(risk.Reasons),
        _ => null
    };

    static string? FirstCause(IEnumerable<string> codes) =>
        codes.Select(Cause).FirstOrDefault(static cause => cause is not null);

    static string? CommonSelectionCause(IEnumerable<string> codes)
    {
        var distinct = codes.Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return distinct.Length == 1 ? Cause(distinct[0]) : null;
    }

    static string? VolatilityRestriction(IntrinsicTimeStrategyWorkflowView view, VolatilityRegimeResult volatility)
    {
        if (volatility.Evidence.Any(item => item.EvidenceId == "VX_TERM_STRUCTURE" && item.IsAvailable
            && item.Value >= view.RegimeDiscoveryParameterSet.Volatility.SevereBackwardationRatio))
            return "VX futures backwardation exceeded the entry limit";
        if (volatility.Reasons.Any(reason => reason.Code == "RD.VOL.EXTREME"))
            return "volatility was extreme";
        return FirstCause(volatility.Reasons.Select(reason => reason.Code));
    }

    static string Direction(RegimeDirection direction) => direction switch
    {
        RegimeDirection.Up => "an upward direction",
        RegimeDirection.Down => "a downward direction",
        RegimeDirection.Neutral => "a neutral direction",
        _ => "no confirmed direction"
    };

    static string Strategy(IntrinsicTimeStrategyWorkflowView view, string market)
    {
        var candidate = view.TradeSelection.Result?.SelectionResult?.SelectedCandidate;
        var definitions = view.SelectionBinding?.CatalogDefinitions;
        var name = candidate is null ? null
            : definitions?.FirstOrDefault(definition => definition.Key == candidate.VariantKey)?.Name
                ?? definitions?.FirstOrDefault(definition => definition.Key == candidate.StrategyKey)?.Name;
        return string.IsNullOrWhiteSpace(name) ? market : $"{name} on {market}";
    }

    static string? Cause(string? code)
    {
        const string assessment = "MC.ASSESSMENT.";
        if (code?.StartsWith(assessment, StringComparison.Ordinal) == true)
        {
            var observation = code[assessment.Length..].Split('.');
            if (observation.Length == 2 && observation[1] is "STALE" or "MISSING")
            {
                var name = observation[0] switch
                {
                    "REFERENCEQUOTE" => "the futures quote",
                    "LASTTRADE" => "the latest futures trade",
                    "NORMALIZEDMOVEMENT" => "the one-minute ATR",
                    "VOLATILITYCHANGE" => "the volatility-change observation",
                    "FEEDHEALTH" => "the feed-health observation",
                    "SESSIONCALENDAR" => "the trading-session calendar",
                    "EVENTRISKCALENDAR" => "the economic-event calendar",
                    "REGIME" => "the regime assessment",
                    _ => null
                };
                if (name is not null)
                    return $"{name} {(observation[1] == "STALE" ? "was stale" : "was unavailable")}";
            }
        }
        return code switch
        {
            "RD.VOL.BACKWARDATION" => "VX futures were in backwardation",
            "RD.VOL.EXTREME" => "volatility was extreme",
            "RD.VOL.HIGH" => "volatility was high",
            "MC.ASSESSMENT.CALENDAR.COVERAGE_UNCONFIRMED" => "economic-event calendar coverage was unconfirmed",
            "MC.ASSESSMENT.UNAVAILABLE" => "required market observations were unavailable",
            "MC.ASSESSMENT.QUOTE.CROSSED" => "the futures bid was above the ask",
            "MC.ASSESSMENT.QUOTE.LOCKED" => "the futures bid and ask were equal",
            "MC.ASSESSMENT.FEED.UNAVAILABLE" => "the market data feed was unavailable",
            "MC.ASSESSMENT.TRIGGER.STALE" => "the triggering ITI signal was stale",
            "TS.NO_AUTHORIZED_CANDIDATE" => "the Fund had no authorized strategy",
            "TS.NO_COMPATIBLE_CANDIDATE" => "no configured strategy matched the market conditions",
            "TS.ASSESSMENT.SESSION" or "OC.MARKET.SESSION_CLOSED" => "the session did not allow new entries",
            "TS.ASSESSMENT.LIQUIDITY" => "liquidity did not meet the entry rules",
            "TS.ASSESSMENT.STRESS" => "market stress exceeded the entry limit",
            "TS.ASSESSMENT.EVENT" => "economic-event risk exceeded the entry limit",
            "TS.ASSESSMENT.DATA_QUALITY" or "TS.REGIME.QUALITY" => "market data did not meet the entry rules",
            "TS.ASSESSMENT.CONFIDENCE" or "TS.REGIME.CONFIDENCE" => "assessment confidence was below the entry minimum",
            "TS.ASSESSMENT.TRIGGER" => "the ITI signal did not meet the entry rules",
            "TS.ASSESSMENT.CONDITION" or "TS.VARIANT.CONDITION" => "the market condition did not match the entry rules",
            "TS.ASSESSMENT.VOLATILITY" or "TS.VARIANT.VOLATILITY" or "TS.REGIME.VOLATILITY_LEVEL"
                or "TS.REGIME.VOLATILITY_CHANGE" => "volatility did not match the entry rules",
            "TS.PERMISSION.OPERATING_STATE" => "the Fund was disabled for new entries",
            "TS.PERMISSION.DIRECTION" => "the Fund did not allow that trade direction",
            "TS.PERMISSION.PRODUCT" => "the Fund did not allow that product",
            "TS.PERMISSION.CONDITION" => "the Fund did not permit trading in those market conditions",
            "TS.PERMISSION.DEPLOYMENT" or "TS.PERMISSION.ENVELOPE" => "the Fund did not authorize that strategy",
            "TS.REGIME.DIRECTION" or "TS.VARIANT.DIRECTION" => "market direction did not match the entry rules",
            "TS.REGIME.PHASE" or "TS.VARIANT.PHASE" => "the trend phase did not match the entry rules",
            "TS.REGIME.STRENGTH" or "TS.VARIANT.STRENGTH" => "trend strength did not meet the entry rules",
            "TS.REGIME.STRUCTURE" or "TS.VARIANT.STRUCTURE" => "market structure did not match the entry rules",
            "TS.CONFIG.INVALID" => "the strategy configuration was invalid",
            "OC.CANDIDATE.NO_CONTRACT" => "no contract combination met the construction rules",
            "OC.MARKET.STALE" => "the candidate quotes were stale",
            "OC.MARKET.SKEW" => "the leg quotes were too far apart in time",
            "OC.MARKET.RECOVERING" => "the market data feed was recovering",
            "OC.MARKET.REFERENCE_UNAVAILABLE" or "OC.PRICING.REFERENCE_UNAVAILABLE" => "required pricing reference data was unavailable",
            "OC.RESULT.INVALID" or "RM.RESULT.INVALID" => "the result did not match the accepted request",
            "RM.CAPACITY.NO_FEASIBLE_QUANTITY" => "the Fund lacked capacity for one unit",
            "RM.CAPACITY.CONTENTION_EXHAUSTED" => "available Fund capacity changed during sizing",
            "RM.MARKET.NEW_ENTRY_BLOCKED" => "the assessed market restrictions blocked new entries",
            "RM.PORTFOLIO.NO_TRADE_ORDERS" => "Portfolio did not authorize any orders",
            _ => null
        };
    }

    static string Words<T>(T value) where T : struct, Enum => WordBoundary().Replace(value.ToString(), " ");

    static string? Prose(string? text)
    {
        var line = text?.Split(['\r', '\n'], 2)[0].Trim();
        return string.IsNullOrWhiteSpace(line) || !line.Contains(' ') ? null : line;
    }

    static string ShortSentence(string sentence)
    {
        var first = SentenceBoundary().Split(sentence, 2)[0].Trim().TrimEnd('.', '!', '?');
        if (first.Length > 200)
        {
            var lastSpace = first.LastIndexOf(' ', 199);
            first = first[..(lastSpace > 120 ? lastSpace : 199)].TrimEnd(',', ':', ';');
            return $"{first}…";
        }
        return $"{first}.";
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();

    [GeneratedRegex("(?<=[.!?])\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBoundary();
}
