using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Contains one page of Strategy Updates rows and its pagination metadata.</summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyUpdatesGridPage(
    [property: Key(0)] IntrinsicTimeStrategyUpdatesGridRow[] Items,
    [property: Key(1)] int PageNumber,
    [property: Key(2)] int PageSize,
    [property: Key(3)] int TotalCount);

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
    /// <summary>Extracts only grid information from the persisted workflow projection.</summary>
    public static IntrinsicTimeStrategyUpdatesGridRow FromView(IntrinsicTimeStrategyWorkflowView view) => new(
        view.WorkflowId, view.WorkflowRevision,
        view.TriggerEvent.CreatedOn == default ? view.StartedAtUtc : view.TriggerEvent.CreatedOn,
        view.Status.ToString(), view.Outcome.ToString(), view.TriggerEvent.FuturesItiSignal,
        [IntrinsicTimeStrategyUpdatesGridStage.FromState(view.RegimeDiscovery),
         IntrinsicTimeStrategyUpdatesGridStage.FromState(view.MarketCondition),
         IntrinsicTimeStrategyUpdatesGridStage.FromState(view.TradeSelection),
         IntrinsicTimeStrategyUpdatesGridStage.FromState(view.OrderComposition),
         IntrinsicTimeStrategyUpdatesGridStage.FromState(view.RiskManagement)]);
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
    [property: Key(6)] string? ErrorMessage)
{
    internal static IntrinsicTimeStrategyUpdatesGridStage FromState(StrategyWorkflowStageState state)
    {
        var message = state.Failure?.ErrorMessage;
        return new(state.ProcessingStatus.ToString(), state.ContinuationDecision.ToString(),
            state.CompletedAtUtc, state.FailedAtUtc, state.Failure?.ErrorType.ToString(),
            state.Failure?.ErrorCode.ToString(), message is { Length: > 512 } ? message[..512] : message);
    }
}
