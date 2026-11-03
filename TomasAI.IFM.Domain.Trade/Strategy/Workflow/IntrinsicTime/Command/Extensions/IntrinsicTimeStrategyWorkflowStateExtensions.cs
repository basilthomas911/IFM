using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;

/// <summary>Enforces the invariant that a preflighted workflow snapshot must be accepted by state.</summary>
internal static class IntrinsicTimeStrategyWorkflowStateExtensions
{

    extension(IntrinsicTimeStrategyWorkflowCommandState state)
    {
        /// <summary>Applies a workflow snapshot or raises an invariant failure before the command can report success.</summary>
        internal void UpdateRequired(
            WorkflowStrategyStateUpdatedEvent domainEvent,
            ICommand command)
        {
            if (!state.Update(domainEvent, command))
                throw new InvalidOperationException("WORKFLOW.STATE.APPLY_FAILED");
        }
    }
}