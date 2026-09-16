using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Model;

internal static class TradeOrderTransition
{
    internal static ServiceResult<GuidResult> Execute(CreateTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.Create(command.Order));
    internal static ServiceResult<GuidResult> Execute(AmendTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.Amend(command.Order));
    internal static ServiceResult<GuidResult> Execute(ApproveTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Approve());
    internal static ServiceResult<GuidResult> Execute(ReadyTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Ready());
    internal static ServiceResult<GuidResult> Execute(BindTradeOrderExecutionCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.BindExecution(
            command.ExecutionAttemptId, command.ExecutionChannel, command.EffectiveAtUtc),
            command.ExecutionAttemptId, command.ExecutionChannel);
    internal static ServiceResult<GuidResult> Execute(ReleaseTradeOrderExecutionCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.ReleaseExecution(
            command.ExecutionAttemptId, command.ZeroExposureConfirmed, command.EffectiveAtUtc),
            command.ExecutionAttemptId);
    internal static ServiceResult<GuidResult> Execute(CompleteTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Complete());
    internal static ServiceResult<GuidResult> Execute(CancelTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Cancel());
    internal static ServiceResult<GuidResult> Execute(ExpireTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.Expire(command.EffectiveAtUtc));

    static ServiceResult<GuidResult> Apply(ICommand<TradeOrderId> command, TradeOrderCommandState state,
        Func<TradeOrderActorStateMachine, TradeDecision<TradeOrderDefinition>> transition,
        Guid executionAttemptId = default, ExecutionChannel executionChannel = default)
    {
        var machine = new TradeOrderActorStateMachine();
        if (state.Current is { } current) machine.Replay(current);
        var decision = transition(machine);
        if (!decision.Accepted || decision.Value is null)
            return TradeCommandResult.Rejected(((ICommand)command).ErrorCode, decision);
        if (!state.Update(new TradeOrderChangedEvent
        {
            EntityId = command.EntityId,
            State = decision.Value,
            ExecutionAttemptId = executionAttemptId,
            ExecutionChannel = executionChannel
        }, (ICommand)command))
            return new ServiceFailed<GuidResult>(((ICommand)command).ErrorCode, "TRADE.ORDER.STATE.APPLY_FAILED");
        return TradeCommandResult.Accepted(((ICommand)command).CommandId);
    }
}
