using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using System.Diagnostics;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using System.Collections.Frozen;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Validation;

/// <summary>Pure workflow command validation before loading a workflow snapshot.</summary>
public static class IntrinsicTimeWorkflowCommandValidation
{
    private static readonly IntrinsicTimeStrategyWorkflowEntityIdValidationRules EntityRules = new();

    /// <summary>Applies all intrinsic workflow entity rules, including its ITI signal identity.</summary>
    public static List<ValidationError> ValidateWorkflowEntityId(this List<ValidationError> errors, IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        errors.AddRange(EntityRules.Execute(entityId));
        return errors;
    }

    /// <summary>Appends workflow payload and cross-identity failures without throwing or mutating state.</summary>
    public static List<ValidationError> ValidateWorkflowCommand(this List<ValidationError> errors, ICommand<IntrinsicTimeStrategyWorkflowEntityId> command)
    {
        if (string.IsNullOrWhiteSpace(command.Subject.EntityId))
            errors.Add(new("Workflow commands require an entity routing identity."));
        if (command is ICommand<IntrinsicTimeStrategyWorkflowEntityId> entityCommand &&
            !string.Equals(command.Subject.EntityId, entityCommand.EntityId.Format(), StringComparison.Ordinal))
            errors.Add(new("Workflow command subject must match its entity identity."));

        if (command is ExecuteIntrinsicTimeStrategyWorkflowCommand execute)
        {
            if (execute.ProposedWorkflowId.Value == Guid.Empty || execute.TriggerEventId == Guid.Empty ||
                execute.CorrelationId == Guid.Empty || execute.CausationId == Guid.Empty ||
                execute.RequestedAtUtc.Kind != DateTimeKind.Utc || execute.WorkflowDefinitionVersion <= 0 ||
                execute.TriggerEvent is null || execute.TriggerEvent.EntityId != execute.EntityId.ItiSignalEntityId)
                errors.Add(new("Workflow start requires valid workflow, trigger, trace, time, and routing identities."));
        }

        var completionResult = command switch
        {
            CompleteRegimeDiscoveryCommand value => value.Result,
            CompleteMarketConditionCommand value => value.Result,
            CompleteTradeSelectionCommand value => value.Result,
            CompleteOrderCompositionCommand value => value.Result,
            CompleteRiskManagementCommand value when value.PortfolioDecision is null => value.Result,
            _ => null
        };
        if (completionResult is not null)
        {
            var resultErrors = StrategyStageResultEnvelopeValidationRules.WithMaximumPayloadBytes(command is CompleteTradeSelectionCommand ? 524288 : StrategyStageResultEnvelope.DefaultMaximumPayloadBytes).Execute(completionResult);
            errors.AddRange(resultErrors);
        }

        if (command is PrepareRiskManagementCommand preparation &&
            (preparation.WorkflowId.Value == Guid.Empty || preparation.InputWorkflowRevision < 1))
            errors.Add(new("Exact Risk preparation identity is required."));
        if (command is AcceptOrderCompositionPreparationCommand acceptance &&
            (acceptance.WorkflowId.Value == Guid.Empty || acceptance.InputWorkflowRevision < 1 || acceptance.Evidence is null ||
             acceptance.Evidence.WorkflowId != acceptance.WorkflowId.Value || acceptance.Evidence.PreparationRevision != acceptance.InputWorkflowRevision))
            errors.Add(new("Exact preparation identity is required."));
        if (command is AdvanceRiskFinancialHandoffCommand handoff &&
            (handoff.WorkflowId.Value == Guid.Empty || handoff.InputWorkflowRevision < 1 || !Enum.IsDefined(handoff.ExpectedPhase)))
            errors.Add(new("Exact financial handoff identity and checkpoint are required."));
        if (completionResult is null && command is CompleteRegimeDiscoveryCommand or CompleteMarketConditionCommand or CompleteTradeSelectionCommand or CompleteOrderCompositionCommand)
            errors.Add(new("Completion Result is required."));
        return errors;
    }
}
