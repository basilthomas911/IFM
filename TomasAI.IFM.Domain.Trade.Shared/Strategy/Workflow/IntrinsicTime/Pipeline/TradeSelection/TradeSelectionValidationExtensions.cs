using FluentValidation;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

/// <summary>Ordered command-list validation for selector ingress and exact catalog capability evidence.</summary>
public static class TradeSelectionValidationExtensions
{
    /// <summary>Aggregates independent required-field errors before inspecting nested frozen evidence.</summary>
    public static List<ValidationError> ValidateSelectionFields(this List<ValidationError> errors, ExecuteTradeSelectionPipelineCommand request)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.AddRange(new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(request.WorkflowEntityId));
        errors.AddRange(new CommandRules().Execute(request));
        return errors;
    }

    /// <summary>Validates relationships and deterministic evidence after required fields have passed.</summary>
    public static List<ValidationError> ValidateSelectionConsistency(this List<ValidationError> errors, ExecuteTradeSelectionPipelineCommand request)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count != 0) return errors;
        try { TradeSelectionContracts.ValidateRequestEvidence(request, errors); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or MessagePack.MessagePackSerializationException)
        { errors.Add(new(ex is TradeSelectionValidationException ? ex.Message : "TS.CONTRACT.INVALID: " + ex.Message)); }
        return errors;
    }

    /// <summary>Validates registered implementations for every pinned capability and parameter validator.</summary>
    public static List<ValidationError> ValidateSelectionCapabilities(this List<ValidationError> errors,
        ExecuteTradeSelectionPipelineCommand request, IStrategyCatalogCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count != 0) return errors;
        var graph = request.SelectionBinding.CatalogDefinitions.ToDictionary(x => x.Key, SelectionCatalogTransport.ToSource);
        try
        {
            foreach (var node in graph.Values)
            {
                foreach (var capability in node.Definition.Capabilities) capabilities.Validate(capability, node.Definition, graph);
                if (node.Definition.Key.Kind == StrategyCatalogKind.ParameterSet)
                    foreach (var validator in graph[node.Definition.Parent!].Definition.Capabilities.Where(x => x.Role == "validator"))
                        capabilities.Validate(validator, node.Definition, graph);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { errors.Add(new(ex is TradeSelectionValidationException ? ex.Message : "TS.CONFIG.CAPABILITY_UNSUPPORTED: " + ex.Message)); }
        return errors;
    }

    // Local adapters do not register duplicate validators for shared workflow/trigger contracts.
    sealed class CommandRules : BaseValidationRules
    {
        public ValidationError[] Execute(ExecuteTradeSelectionPipelineCommand request) => Validate(request, new Rules());
        sealed class Rules : AbstractValidator<ExecuteTradeSelectionPipelineCommand>
        {
            public Rules()
            {
                RuleFor(x => x.SchemaVersion).Equal((short)1);
                RuleFor(x => x.PostEvents).Equal(false);
                RuleFor(x => x.WorkflowId.Value).NotEmpty();
                RuleFor(x => x.InputWorkflowRevision).GreaterThan(0);
                RuleFor(x => x.CorrelationId).NotEmpty();
                RuleFor(x => x.CausationId).NotEmpty();
                RuleFor(x => x.RequestedAtUtc).Must(TradeSelectionContracts.Utc);
                RuleFor(x => x.EvaluatedAtUtc).Must(TradeSelectionContracts.Utc);
                RuleFor(x => x.ExpiresAtUtc).Must(TradeSelectionContracts.Utc);
                RuleFor(x => x.WorkflowView).NotNull();
                RuleFor(x => x.TriggerEvent).NotNull();
                RuleFor(x => x.SelectionBinding).NotNull();
                RuleFor(x => x.RegimeResultEnvelope).NotNull();
                RuleFor(x => x.AssessmentResultEnvelope).NotNull();
                When(x => x.WorkflowView is not null, () =>
                {
                    RuleFor(x => x.WorkflowView.TriggerEvent).NotNull();
                    RuleFor(x => x.WorkflowView.RegimeDiscovery).NotNull();
                    RuleFor(x => x.WorkflowView.MarketCondition).NotNull();
                    RuleFor(x => x.WorkflowView.TradeSelection).NotNull();
                });
                When(x => x.SelectionBinding is not null, () =>
                {
                    RuleFor(x => x.SelectionBinding.CommonPolicy).NotNull();
                    RuleFor(x => x.SelectionBinding.CatalogDefinitions).Must(x => x.All(n => n is not null));
                    RuleFor(x => x.SelectionBinding.Candidates).Must(x => x.All(n => n is not null));
                });
            }
        }
    }
}
