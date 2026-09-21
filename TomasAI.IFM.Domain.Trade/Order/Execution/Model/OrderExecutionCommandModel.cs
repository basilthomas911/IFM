using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Model;

internal static class OrderExecutionCommandModel
{
    internal static ServiceResult<GuidResult> Apply(ICommand<OrderExecutionId> command,
        OrderExecutionCommandState state,
        Func<OrderExecutionActorStateMachine, TradeDecision<OrderExecutionDefinition>> transition)
    {
        var machine = CreateMachine(state);
        var result = transition(machine);
        var raw = (ICommand)command;
        if (!result.Accepted || result.Value is null)
            return TradeCommandResult.Rejected(raw.ErrorCode, result);
        if (ReferenceEquals(result.Value, state.Current))
            return TradeCommandResult.Accepted(raw.CommandId);
        var applied = state.Update(new OrderExecutionChangedEvent
        {
            EntityId = command.EntityId,
            State = result.Value
        }, raw);
        return applied
            ? TradeCommandResult.Accepted(raw.CommandId)
            : new ServiceFailed<GuidResult>(raw.ErrorCode, "OE.STATE.APPLY_FAILED");
    }

    internal static ServiceResult<GuidResult> Accept(AcceptOrderExecutionCommand command,
        OrderExecutionCommandState state)
    {
        var machine = CreateMachine(state);
        var accepted = machine.Accept(command.EffectiveAtUtc);
        if (!accepted.Accepted || accepted.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, accepted);
        var current = machine.Current!;
        var applied = state.Update(new OrderExecutionChangedEvent
        {
            EntityId = command.EntityId,
            State = current,
            CreatedTrades = accepted.Value.CreatedTrades,
            ClosedPositions = accepted.Value.ClosedPositions
        }, command);
        return applied
            ? TradeCommandResult.Accepted(command.CommandId)
            : new ServiceFailed<GuidResult>(command.ErrorCode, "OE.STATE.APPLY_FAILED");
    }

    internal static ServiceResult<GuidResult> Cancel(CancelOrderExecutionCommand command,
        OrderExecutionCommandState state)
    {
        var machine = CreateMachine(state);
        var accepted = machine.Cancel(command.EffectiveAtUtc);
        if (!accepted.Accepted || accepted.Value is null)
            return TradeCommandResult.Rejected(command.ErrorCode, accepted);
        var applied = state.Update(new OrderExecutionChangedEvent
        {
            EntityId = command.EntityId,
            State = machine.Current!,
            CreatedTrades = accepted.Value.CreatedTrades,
            ClosedPositions = accepted.Value.ClosedPositions
        }, command);
        return applied
            ? TradeCommandResult.Accepted(command.CommandId)
            : new ServiceFailed<GuidResult>(command.ErrorCode, "OE.STATE.APPLY_FAILED");
    }

    private static OrderExecutionActorStateMachine CreateMachine(OrderExecutionCommandState state)
    {
        var machine = new OrderExecutionActorStateMachine();
        if (state.Current is { } current) machine.Replay(current);
        return machine;
    }
}
