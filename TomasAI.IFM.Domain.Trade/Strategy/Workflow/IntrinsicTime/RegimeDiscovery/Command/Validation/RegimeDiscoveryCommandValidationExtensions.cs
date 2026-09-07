using FluentValidation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Command.Validation;

/// <summary>Deterministic ingress checks for one Regime Discovery Function command.</summary>
public static class RegimeDiscoveryCommandValidationExtensions
{
    public static List<ValidationError> ValidateRegimeDiscoveryRevision(this List<ValidationError> errors, long value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value <= 0) errors.Add(new("InputWorkflowRevision must be positive."));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryTraceId(
        this List<ValidationError> errors, Guid value, string name)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value == Guid.Empty) errors.Add(new($"{name} is required."));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryTimestamp(
        this List<ValidationError> errors, DateTime value, string name)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value == default || value.Kind != DateTimeKind.Utc)
            errors.Add(new($"{name} must be a non-default UTC timestamp."));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryTargetHorizon(
        this List<ValidationError> errors, TimeFrameType value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            errors.Add(new("TargetHorizon must be Daily, Weekly, or Monthly."));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryParameterHash(
        this List<ValidationError> errors, string? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!IsSha256(value))
            errors.Add(new("ParameterPayloadSha256 must contain exactly 64 hexadecimal characters."));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryWorkflowView(
        this List<ValidationError> errors, IntrinsicTimeStrategyWorkflowView? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null) errors.Add(new("WorkflowView is required."));
        else errors.AddRange(new WorkflowViewRules().Execute(value));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryTrigger(
        this List<ValidationError> errors, FuturesItiSignalGeneratedEvent? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null) errors.Add(new("TriggerEvent is required."));
        else errors.AddRange(new TriggerRules().Execute(value));
        return errors;
    }

    public static List<ValidationError> ValidateRegimeDiscoveryConsistency(
        this List<ValidationError> errors, ExecuteRegimeDiscoveryPipelineCommand request)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var view = request.WorkflowView;
        if (view is not null && (view.EntityId != request.WorkflowEntityId ||
            view.WorkflowId != request.WorkflowId || view.WorkflowRevision != request.InputWorkflowRevision))
            errors.Add(new("WorkflowView identity and revision must match the Regime Discovery execution."));
        if (request.ExpiresAtUtc <= request.RequestedAtUtc)
            errors.Add(new("ExpiresAtUtc must be later than RequestedAtUtc."));
        if (request.ParameterSet is { } parameters && request.TargetHorizon != parameters.TargetHorizon)
            errors.Add(new("TargetHorizon must match the parameter-set target horizon."));
        if (request.TriggerEvent?.EntityId is { } triggerId && request.WorkflowEntityId.ItiSignalEntityId is { } workflowTriggerId &&
            (request.TargetHorizon != triggerId.TimePeriod || workflowTriggerId != triggerId))
            errors.Add(new("TargetHorizon and workflow identity must match the trigger ITI timeframe and identity."));
        if (IsSha256(request.ParameterPayloadSha256) && request.ParameterSet is not null &&
            !string.Equals(request.ParameterPayloadSha256,
                RegimeDiscoveryParameterPayload.ComputeSha256(request.ParameterSet), StringComparison.OrdinalIgnoreCase))
            errors.Add(new("ParameterPayloadSha256 must match the canonical parameter payload."));
        return errors;
    }

    static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    // These are command-specific views of structured upstream payloads. Stage snapshots, fund/catalog
    // bindings, provenance and optional signal data retain their full upstream range here; Regime
    // Discovery validates its identity/revision inputs and evaluates signal quality during execution.
    sealed class WorkflowViewRules : BaseValidationRules, IValidationRules<IntrinsicTimeStrategyWorkflowView>
    {
        static readonly WorkflowValidator Rules = new();
        public ValidationError[] Execute(IntrinsicTimeStrategyWorkflowView value) => Validate(value, Rules);
        sealed class WorkflowValidator : AbstractValidator<IntrinsicTimeStrategyWorkflowView>
        {
            public WorkflowValidator()
            {
                RuleFor(x => x.EntityId).Custom((id, context) =>
                {
                    foreach (var error in new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(id))
                        context.AddFailure(error.ErrorMessage);
                });
                RuleFor(x => x.WorkflowId).Custom((id, context) =>
                {
                    foreach (var error in new StrategyWorkflowIdValidationRules().Execute(id))
                        context.AddFailure(error.ErrorMessage);
                });
                RuleFor(x => x.WorkflowRevision).GreaterThan(0);
            }
        }
    }

    sealed class TriggerRules : BaseValidationRules, IValidationRules<FuturesItiSignalGeneratedEvent>
    {
        static readonly TriggerValidator Rules = new();
        public ValidationError[] Execute(FuturesItiSignalGeneratedEvent value) => Validate(value, Rules);
        sealed class TriggerValidator : AbstractValidator<FuturesItiSignalGeneratedEvent>
        {
            public TriggerValidator()
            {
                RuleFor(x => x.EntityId).NotNull().DependentRules(() =>
                {
                    RuleFor(x => x.EntityId.ContractId).NotEmpty();
                    RuleFor(x => x.EntityId.ValueDate).Must(date => date != DateOnly.MinValue && date != DateOnly.MaxValue);
                    RuleFor(x => x.EntityId.TimePeriod)
                        .Must(value => value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly);
                });
            }
        }
    }
}
