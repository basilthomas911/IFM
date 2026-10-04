using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

/// <summary>Handles SubmitOrderExecution through computation, guards and state-owned event application.</summary>
public static class SubmitOrderExecution
{
    /// <summary>Applies a valid execution decision; unchanged evidence adds no new event.</summary>
    /// <param name="command">The concrete execution intent.</param>
    /// <param name="state">The authoritative execution state.</param>
    /// <returns>The command ID on success, or the business/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this SubmitOrderExecutionCommand command, OrderExecutionCommandState state)
    {
        var errorMsg = "OrderExecution.STATE.APPLY_FAILED";
        var computed = command.Compute(state.OrderExecutionDefinition, out var executionChange);
        if (executionChange.Accepted && executionChange.IsValidFor(command.EntityId) &&
            ReferenceEquals(executionChange.OrderExecutionDefinition, state.OrderExecutionDefinition))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var updated = computed switch
        {
            _ when !executionChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{executionChange.RejectionCode};{executionChange.RejectionReason}"),
            _ when !executionChange.IsValidFor(command.EntityId)
                => command.UpdateFailed(ref errorMsg, "OrderExecution.COMPUTED_EXECUTION.INVALID"),
            _ => state.Update(command.CreateOrderExecutionChangedEvent(executionChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes proposed execution data without changing actor state or its input.</summary>
    /// <param name="command">The proposed execution intent.</param>
    /// <param name="orderExecutionDefinition">The current execution definition, if any.</param>
    /// <param name="executionChange">The proposed execution data or business rejection.</param>
    /// <returns>True when computation accepts the intent.</returns>
    internal static bool Compute(this SubmitOrderExecutionCommand command, OrderExecutionDefinition? orderExecutionDefinition,
        out OrderExecutionCompute executionChange)
    {
        executionChange = OrderExecutionComputation.Move(orderExecutionDefinition, [OrderExecutionStatus.Pending], OrderExecutionStatus.Submitted, "submit");
        return executionChange.Accepted;
    }

    /// <summary>Creates the source event carrying guarded execution and establishment evidence.</summary>
    /// <param name="command">The originating command identity and route.</param>
    /// <param name="executionChange">The accepted business computation.</param>
    /// <returns>The private event applied and persisted through State.Update.</returns>
    internal static OrderExecutionChangedEvent CreateOrderExecutionChangedEvent(this SubmitOrderExecutionCommand command,
        OrderExecutionCompute executionChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, OrderExecutionChangedEvent.Actor, OrderExecutionChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        OrderExecutionDefinition = executionChange.OrderExecutionDefinition!,
        ReceivedOn = DateTime.UtcNow,
        CreatedTrades = executionChange.CreatedTrades,
        ClosedPositions = executionChange.ClosedPositions
    };
}
