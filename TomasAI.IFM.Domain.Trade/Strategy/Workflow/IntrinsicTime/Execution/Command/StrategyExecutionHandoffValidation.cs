using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;

/// <summary>Checks that each handoff command addresses its original execution identity.</summary>
public static class StrategyExecutionHandoffValidation
{
    extension(List<ValidationError> errors)
    {
        public List<ValidationError> ValidateHandoffIdentity(ICommand command)
        {
            if (command is not ICommand<OrderExecutionId> scoped || !scoped.EntityId.IsValid)
                errors.Add(new("Exact handoff execution identity is required."));
            else if (command.Subject.ActorType != ActorType.Command || command.Subject.Name != BeginStrategyExecutionHandoffCommand.Actor ||
                command.Subject.EntityId != scoped.EntityId.Format() || command.Subject.Verb !=
                    (command is BeginStrategyExecutionHandoffCommand ? BeginStrategyExecutionHandoffCommand.Verb : AdvanceStrategyExecutionHandoffCommand.Verb))
                errors.Add(new("Handoff subject, verb and execution identity must agree."));
            return errors;
        }
    }
}
