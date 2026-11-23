using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Contains one page of Strategy Updates rows and its pagination metadata.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyUpdatesGridPage(
    [property: Key(0)] IntrinsicTimeStrategyUpdatesGridRow[] Items,
    [property: Key(1)] int PageNumber,
    [property: Key(2)] int PageSize,
    [property: Key(3)] int TotalCount)
{
    [Key(4)] public IntrinsicTimeFundAssignment[]? Assignments { get; init; }
    [Key(5)] public string[]? Contracts { get; init; }
    [Key(6)] public bool HasMore { get; init; }
}

/// <summary>Contains the Strategy Updates columns, chart correlation, and detail selection identity.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyUpdatesGridRow(
    [property: Key(0)] StrategyWorkflowId WorkflowId,
    [property: Key(1)] long WorkflowRevision,
    [property: Key(2)] DateTime OccurredAtUtc,
    [property: Key(3)] string Status,
    [property: Key(4)] string Outcome,
    [property: Key(5)] FuturesItiSignalV2ReadModel? Signal,
    [property: Key(6)] IntrinsicTimeStrategyUpdatesGridStage[] Stages)
{
    [Key(7)] public long SignalSequence { get; init; }
    [Key(8)] public bool LegacyUnassigned { get; init; }
    [Key(9)] public DateTime? HistoryOrderAtUtc { get; init; }

    public static DateTime SignalTime(IntrinsicTimeStrategyWorkflowView view)
    {
        var at = view.TriggerEvent.CreatedOn != default ? view.TriggerEvent.CreatedOn :
            view.TriggerEvent.ReceivedOn != default ? view.TriggerEvent.ReceivedOn : view.StartedAtUtc;
        return at.AddTicks(-(at.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    /// <summary>Extracts only grid information from the persisted workflow projection.</summary>
    public static IntrinsicTimeStrategyUpdatesGridRow FromView(IntrinsicTimeStrategyWorkflowView view) => new(
        view.WorkflowId, view.WorkflowRevision,
        SignalTime(view),
        view.Status.ToString(), view.Outcome.ToString(), view.TriggerEvent.FuturesItiSignal,
        [ProjectStage(view, StrategyWorkflowStage.RegimeDiscovery, view.RegimeDiscovery),
         ProjectStage(view, StrategyWorkflowStage.MarketCondition, view.MarketCondition),
         ProjectStage(view, StrategyWorkflowStage.TradeSelection, view.TradeSelection),
         ProjectStage(view, StrategyWorkflowStage.OrderComposition, view.OrderComposition),
         ProjectStage(view, StrategyWorkflowStage.RiskManagement, view.RiskManagement)])
        { SignalSequence = view.TriggerEvent.EventId };

    static IntrinsicTimeStrategyUpdatesGridStage ProjectStage(IntrinsicTimeStrategyWorkflowView view,
        StrategyWorkflowStage stage, StrategyWorkflowStageState state) =>
        IntrinsicTimeStrategyUpdatesGridStage.FromState(state,
            stage == StrategyWorkflowStage.MarketCondition && view.Outcome == StrategyWorkflowOutcome.NoTrade
                ? view.RegimeDiscovery.Result?.RegimeResult : null,
            view.StopReasonCode) with
        {
            ReasonSentence = stage == StrategyWorkflowStage.MarketCondition
                ? StrategyWorkflowStageReasons.Describe(view, stage, state)
                : state.ReasonSentence ?? StrategyWorkflowStageReasons.Describe(view, stage, state)
        };
}

/// <summary>Contains the status and tooltip fields for one pipeline indicator.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyUpdatesGridStage(
    [property: Key(0)] string Processing,
    [property: Key(1)] string Continuation,
    [property: Key(2)] DateTime? Completed,
    [property: Key(3)] DateTime? Failed,
    [property: Key(4)] string? ErrorType,
    [property: Key(5)] string? ErrorCode,
    [property: Key(6)] string? ErrorMessage,
    [property: Key(7)] string[]? DecisionReasons = null,
    [property: Key(8)] string? ReasonSentence = null)
{
    internal static IntrinsicTimeStrategyUpdatesGridStage FromState(StrategyWorkflowStageState state,
        RegimeDiscoveryResult? regime = null, string? stopReason = null)
    {
        var message = state.Failure?.ErrorMessage;
        var reasons = state.ContinuationDecision == StrategyWorkflowContinuationDecision.Stop
            && state.ProcessingStatus == StrategyActorProcessingStatus.Completed ? state.ContinuationReasonCodes : [];
        if (reasons.Length == 0 && state.ContinuationDecision == StrategyWorkflowContinuationDecision.Stop
            && state.ProcessingStatus == StrategyActorProcessingStatus.Completed && !string.IsNullOrWhiteSpace(stopReason))
            reasons = [stopReason];
        if (reasons.Contains("MC.ASSESSMENT.INHERITED_NO_NEW_TRADE", StringComparer.Ordinal)
            && regime is not null)
            reasons = [.. reasons, .. regime.Volatility.Reasons.Select(reason => reason.Code)];
        return new(state.ProcessingStatus.ToString(), state.ContinuationDecision.ToString(),
            state.CompletedAtUtc, state.FailedAtUtc, state.Failure?.ErrorType.ToString(),
            state.Failure?.ErrorCode.ToString(), message is { Length: > 512 } ? message[..512] : message,
            reasons.Length == 0 ? null : reasons.Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Length > 96 ? code[..96] : code).Distinct(StringComparer.Ordinal).Take(8).ToArray());
    }
}
