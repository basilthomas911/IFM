using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;
/// <summary>Handles AddOrderExecutionFill through computation, guards and state-owned event application.</summary>
public static class AddOrderExecutionFill
{

    /// <param name="command">
    /// The concrete execution intent.
    /// The proposed execution intent.
    /// The originating command identity and route.
    /// </param>
    extension(AddOrderExecutionFillCommand command)
    {
        /// <summary>Applies a valid execution decision; unchanged evidence adds no new event.</summary>
        ///
        /// <param name = "state">The authoritative execution state.</param>
        /// <returns>The command ID on success, or the business/application failure.</returns>
        public ServiceResult<GuidResult> Execute(OrderExecutionCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = "OrderExecution.STATE.APPLY_FAILED";
            var computed = command.Compute(state.OrderExecutionDefinition, out var executionChange);
            if (executionChange.Accepted && executionChange.IsValidFor(command.EntityId) && ReferenceEquals(executionChange.OrderExecutionDefinition, state.OrderExecutionDefinition))
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            var updated = computed switch
            {
                _ when !executionChange.Accepted => command.UpdateFailed(ref errorMsg, $"{executionChange.RejectionCode};{executionChange.RejectionReason}"),
                _ when !executionChange.IsValidFor(command.EntityId) => command.UpdateFailed(ref errorMsg, "OrderExecution.COMPUTED_EXECUTION.INVALID"),
                _ => state.Update(command.CreateOrderExecutionChangedEvent(executionChange), command)
            };
            return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes proposed execution data without changing actor state or its input.</summary>
        ///
        /// <param name = "orderExecutionDefinition">The current execution definition, if any.</param>
        /// <param name = "executionChange">The proposed execution data or business rejection.</param>
        /// <returns>True when computation accepts the intent.</returns>
        internal bool Compute(OrderExecutionDefinition? orderExecutionDefinition, out OrderExecutionCompute executionChange)
        {
            executionChange = OrderExecutionComputation.AddFill(orderExecutionDefinition, command.Fill);
            return executionChange.Accepted;
        }

        /// <summary>Creates the source event carrying guarded execution and establishment evidence.</summary>
        ///
        /// <param name = "executionChange">The accepted business computation.</param>
        /// <returns>The private event applied and persisted through State.Update.</returns>
        internal OrderExecutionChangedEvent CreateOrderExecutionChangedEvent(OrderExecutionCompute executionChange) => new()
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
}
