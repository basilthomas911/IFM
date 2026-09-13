using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.Domain;

namespace TomasAI.IFM.Domain.Trade.Order.Command.Extensions;

public static class TradeOrderCommandHandlers
{
    public static ServiceResult<GuidResult> Execute(this CreateTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.Create(command.Order));
    public static ServiceResult<GuidResult> Execute(this AmendTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.Amend(command.Order));
    public static ServiceResult<GuidResult> Execute(this ApproveTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Approve());
    public static ServiceResult<GuidResult> Execute(this ReadyTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Ready());
    public static ServiceResult<GuidResult> Execute(this BindTradeOrderExecutionCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.BindExecution(
            command.ExecutionAttemptId, command.ExecutionChannel, command.EffectiveAtUtc),
            command.ExecutionAttemptId, command.ExecutionChannel);
    public static ServiceResult<GuidResult> Execute(this ReleaseTradeOrderExecutionCommand command, TradeOrderCommandState state) =>
        Apply(command, state, machine => machine.ReleaseExecution(
            command.ExecutionAttemptId, command.ZeroExposureConfirmed, command.EffectiveAtUtc),
            command.ExecutionAttemptId);
    public static ServiceResult<GuidResult> Execute(this CompleteTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Complete());
    public static ServiceResult<GuidResult> Execute(this CancelTradeOrderCommand command, TradeOrderCommandState state) =>
        Apply(command, state, static machine => machine.Cancel());
    public static ServiceResult<GuidResult> Execute(this ExpireTradeOrderCommand command, TradeOrderCommandState state) =>
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
        state.Update(new TradeOrderChangedEvent
        {
            EntityId = command.EntityId,
            State = decision.Value,
            ExecutionAttemptId = executionAttemptId,
            ExecutionChannel = executionChannel
        }, (ICommand)command);
        return TradeCommandResult.Accepted(((ICommand)command).CommandId);
    }
}
