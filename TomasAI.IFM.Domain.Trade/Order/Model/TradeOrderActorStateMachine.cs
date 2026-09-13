using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Model;

/// <summary>Pure state-transition model used by the TradeOrder command actor and replay tests.</summary>
public sealed class TradeOrderActorStateMachine
{
    public TradeOrderDefinition? Current { get; private set; }

    public TradeDecision<TradeOrderDefinition> Create(TradeOrderDefinition order)
    {
        if (Current is not null) return TradeDecision<TradeOrderDefinition>.Reject("TO.ALREADY_EXISTS", "Trade Order already exists.");
        var errors = order.Validate();
        if (errors.Length > 0) return TradeDecision<TradeOrderDefinition>.Reject("TO.INVALID", string.Join(" | ", errors));
        Current = order with { Status = TradeOrderStatus.Draft };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    public TradeDecision<TradeOrderDefinition> Amend(TradeOrderDefinition replacement)
    {
        if (Current is null) return Missing();
        if (Current.Status != TradeOrderStatus.Draft)
            return RejectTransition("amend");
        if (replacement.Id != Current.Id || replacement.Revision != Current.Revision + 1)
            return TradeDecision<TradeOrderDefinition>.Reject("TO.REVISION", "Replacement must retain identity and increment revision exactly once.");
        var errors = replacement.Validate();
        if (errors.Length > 0) return TradeDecision<TradeOrderDefinition>.Reject("TO.INVALID", string.Join(" | ", errors));
        Current = replacement with { Status = TradeOrderStatus.Draft };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    public TradeDecision<TradeOrderDefinition> Approve() => Move(TradeOrderStatus.Draft, TradeOrderStatus.Approved, "approve");
    public TradeDecision<TradeOrderDefinition> Ready() => Move(TradeOrderStatus.Approved, TradeOrderStatus.Ready, "mark ready");
    public TradeDecision<TradeOrderDefinition> BindExecution(
        Guid executionAttemptId,
        ExecutionChannel channel,
        DateTime boundAtUtc)
    {
        if (Current is null) return Missing();
        if (Current.Status != TradeOrderStatus.Ready) return RejectTransition("bind execution");
        if (executionAttemptId == Guid.Empty || boundAtUtc.Kind != DateTimeKind.Utc)
            return TradeDecision<TradeOrderDefinition>.Reject(
                "TO.INVALID_EXECUTION_BINDING",
                "Execution binding requires a non-empty attempt ID and UTC timestamp.");
        Current = Current with
        {
            Status = TradeOrderStatus.Executing,
            BoundExecutionAttemptId = executionAttemptId,
            BoundExecutionChannel = channel,
            ExecutionBoundAtUtc = boundAtUtc
        };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    public TradeDecision<TradeOrderDefinition> ReleaseExecution(
        Guid executionAttemptId,
        bool zeroExposureConfirmed,
        DateTime releasedAtUtc)
    {
        if (Current is null) return Missing();
        if (Current.Status != TradeOrderStatus.Executing) return RejectTransition("release execution");
        if (!zeroExposureConfirmed || executionAttemptId == Guid.Empty ||
            executionAttemptId != Current.BoundExecutionAttemptId || releasedAtUtc.Kind != DateTimeKind.Utc)
            return TradeDecision<TradeOrderDefinition>.Reject(
                "TO.EXECUTION_RELEASE_NOT_PROVEN",
                "Release requires the bound execution attempt, UTC evidence time, and proven zero exposure.");
        Current = Current with
        {
            Status = TradeOrderStatus.Ready,
            BoundExecutionAttemptId = null,
            BoundExecutionChannel = null,
            ExecutionBoundAtUtc = null
        };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }
    public TradeDecision<TradeOrderDefinition> Complete() => Move(TradeOrderStatus.Executing, TradeOrderStatus.Completed, "complete");

    public TradeDecision<TradeOrderDefinition> Cancel()
    {
        if (Current is null) return Missing();
        if (Current.Status is TradeOrderStatus.Completed or TradeOrderStatus.Cancelled or TradeOrderStatus.Expired)
            return RejectTransition("cancel");
        Current = Current with { Status = TradeOrderStatus.Cancelled };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    public TradeDecision<TradeOrderDefinition> Expire(DateTime nowUtc)
    {
        if (Current is null) return Missing();
        if (nowUtc.Kind != DateTimeKind.Utc || nowUtc < Current.ValidUntilUtc)
            return TradeDecision<TradeOrderDefinition>.Reject("TO.NOT_EXPIRED", "Order validity has not expired.");
        if (Current.Status is TradeOrderStatus.Executing or TradeOrderStatus.Completed)
            return RejectTransition("expire");
        Current = Current with { Status = TradeOrderStatus.Expired };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    public void Replay(TradeOrderDefinition state) => Current = state;

    TradeDecision<TradeOrderDefinition> Move(TradeOrderStatus expected, TradeOrderStatus next, string operation)
    {
        if (Current is null) return Missing();
        if (Current.Status != expected) return RejectTransition(operation);
        Current = Current with { Status = next };
        return TradeDecision<TradeOrderDefinition>.Accept(Current);
    }

    TradeDecision<TradeOrderDefinition> Missing() =>
        TradeDecision<TradeOrderDefinition>.Reject("TO.NOT_FOUND", "Trade Order does not exist.");

    TradeDecision<TradeOrderDefinition> RejectTransition(string operation) =>
        TradeDecision<TradeOrderDefinition>.Reject("TO.INVALID_TRANSITION", $"Cannot {operation} an order in {Current!.Status} state.");
}
