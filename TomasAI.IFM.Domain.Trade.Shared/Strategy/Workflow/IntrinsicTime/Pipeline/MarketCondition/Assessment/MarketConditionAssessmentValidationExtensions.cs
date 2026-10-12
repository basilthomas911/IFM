using TomasAI.IFM.Domain.Trade.Shared;
using FluentValidation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

/// <summary>Ordered, aggregating list validation shared by actor ingress and assessment contract consumers.</summary>
public static class MarketConditionAssessmentValidationExtensions
{
    /// <summary>Validates the intrinsic execution identity.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentIdentity(this List<ValidationError> errors, MarketConditionAssessmentExecutionId id)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.AddRange(new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(id.WorkflowEntityId));
        if (id.WorkflowId.Value == Guid.Empty) errors.Add(new("WorkflowId is required."));
        return errors;
    }

    /// <summary>Validates the committed input revision.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentRevision(this List<ValidationError> errors, long revision)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (revision <= 0) errors.Add(new("InputWorkflowRevision must be positive."));
        return errors;
    }

    /// <summary>Validates a correlation or causation identifier.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentTraceId(this List<ValidationError> errors, Guid id, string name)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (id == Guid.Empty) errors.Add(new($"{name} is required."));
        return errors;
    }

    /// <summary>Validates an explicit UTC instant.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentTimestamp(this List<ValidationError> errors, DateTime at, string name)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!MarketConditionAssessmentContracts.Utc(at)) errors.Add(new($"{name} must be a non-default UTC timestamp."));
        return errors;
    }

    /// <summary>Validates one supported triggering horizon.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentHorizon(this List<ValidationError> errors, TimeFrameType horizon)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!MarketConditionAssessmentParameterSet.IsHorizon(horizon)) errors.Add(new("TargetHorizon must be Daily, Weekly or Monthly."));
        return errors;
    }

    /// <summary>Validates a complete SHA-256 field.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentHash(this List<ValidationError> errors, string? hash, string name)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit)) errors.Add(new($"{name} must contain 64 hexadecimal characters."));
        return errors;
    }

    /// <summary>Appends structured parameter validation errors, including null nested profiles.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentParameters(this List<ValidationError> errors, MarketConditionAssessmentParameterSet? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null) errors.Add(new("ParameterSet is required."));
        else errors.AddRange(new ParameterRules().Execute(value));
        return errors;
    }

    /// <summary>Appends frozen workflow validation errors without dereferencing missing payloads.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentWorkflow(this List<ValidationError> errors, IntrinsicTimeStrategyWorkflowView? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null) errors.Add(new("WorkflowView is required."));
        else errors.AddRange(new WorkflowRules().Execute(value));
        return errors;
    }

    /// <summary>Appends trigger identity validation errors.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentTrigger(this List<ValidationError> errors, FuturesItiSignalGeneratedEvent? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (value is null) errors.Add(new("TriggerEvent is required."));
        else errors.AddRange(new TriggerRules().Execute(value));
        return errors;
    }

    /// <summary>Checks envelope integrity and accepted Regime lineage; only legacy payload decoding can throw.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentUpstream(this List<ValidationError> errors, StrategyStageResultEnvelope? envelope,
        IntrinsicTimeStrategyWorkflowView? view)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (envelope is null) { errors.Add(new("RegimeResultEnvelope is required.")); return errors; }
        errors.AddRange(new StrategyStageResultEnvelopeValidationRules().Execute(envelope));
        if (!string.Equals(envelope.ResultType,nameof(RegimeDiscoveryResult),StringComparison.Ordinal) || !envelope.HasValidPayloadSha256())
        { errors.Add(new("Invalid accepted regime envelope.")); return errors; }
        RegimeDiscoveryResult regime;
        try { regime = envelope.ReadRegimeResult(); }
        catch (Exception exception) when (exception is ArgumentException or MessagePack.MessagePackSerializationException)
        { errors.Add(new("Accepted regime payload cannot be decoded.")); return errors; }
        if (view?.AssessmentBinding?.Parameters is { } parameters &&
            !MarketConditionAssessmentContracts.IsRegimeConsistent(regime, envelope, view, parameters))
            errors.Add(new("Accepted regime lineage or decision is invalid."));
        return errors;
    }

    /// <summary>Checks consistency between individually validated frozen request fields.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the existing concrete serialized collection model contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the existing concrete serialized collection model contract.")]
    public static List<ValidationError> ValidateAssessmentConsistency(this List<ValidationError> errors, ExecuteMarketConditionAssessmentCommand c)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(errors);
        if (c.ParameterSet is not { } parameters || !parameters.IsValid() || c.WorkflowView is not { } v ||
            v.AssessmentBinding is not { Parameters: { } frozen } binding || !frozen.IsValid() ||
            c.TriggerEvent?.EntityId is null || v.TriggerEvent?.EntityId is null || v.RegimeDiscovery is null ||
            c.RegimeResultEnvelope is null) return errors;
        if (c.RequestedAtUtc > DateTime.MaxValue.AddMilliseconds(-parameters.MaximumExecutionMilliseconds))
        { errors.Add(new("RequestedAtUtc leaves no room for the execution budget.")); return errors; }
        if (c.SchemaVersion != 1 || c.CommandId == Guid.Empty || c.WorkflowId.Value == Guid.Empty ||
            c.Subject.ActorType != ActorType.Function || !string.Equals(c.Subject.Name,ExecuteMarketConditionAssessmentCommand.Actor,StringComparison.Ordinal) ||
            !string.Equals(c.Subject.Verb,ExecuteMarketConditionAssessmentCommand.Verb,StringComparison.Ordinal) || !string.Equals(c.Subject.EntityId,c.EntityId.Format(),StringComparison.Ordinal) ||
            c.WorkflowEntityId != v.EntityId || c.WorkflowId != v.WorkflowId || c.InputWorkflowRevision != v.WorkflowRevision ||
            v.Status != WorkflowStrategyMachineStatus.Started || v.CurrentStage != StrategyWorkflowStage.MarketCondition ||
            !MarketConditionAssessmentContracts.Utc(c.RequestedAtUtc) || !MarketConditionAssessmentContracts.Utc(c.ExpiresAtUtc) || c.ExpiresAtUtc <= c.RequestedAtUtc ||
            c.ExpiresAtUtc > v.ExpiresAtUtc || c.ExpiresAtUtc > c.RequestedAtUtc.AddMilliseconds(c.ParameterSet.MaximumExecutionMilliseconds) ||
            !string.Equals(c.MarketProfileId,c.ParameterSet.MarketProfileId,StringComparison.Ordinal) || !string.Equals(c.InstrumentRoot,c.ParameterSet.InstrumentRoot,StringComparison.Ordinal) ||
            c.TargetHorizon != c.ParameterSet.TargetHorizon || c.TargetHorizon != c.TriggerEvent.EntityId.TimePeriod ||
            c.TargetHorizon != v.TriggerEvent.EntityId.TimePeriod || c.TriggerEvent.EntityId != v.TriggerEvent.EntityId ||
            !string.Equals(MarketConditionAssessmentHash.Compute(c.TriggerEvent),MarketConditionAssessmentHash.Compute(v.TriggerEvent),StringComparison.Ordinal) ||
            !string.Equals(c.ParameterPayloadSha256,MarketConditionAssessmentHash.Parameters(c.ParameterSet),StringComparison.Ordinal) ||
            !string.Equals(c.ParameterPayloadSha256,binding.PayloadSha256,StringComparison.Ordinal) || !string.Equals(c.ParameterPayloadSha256,MarketConditionAssessmentHash.Parameters(binding.Parameters),StringComparison.Ordinal) ||
            v.RegimeDiscovery.ProcessingStatus != StrategyActorProcessingStatus.Completed || v.RegimeDiscovery.CompletedAtUtc is null ||
            v.RegimeDiscovery.Result is not { } accepted || !accepted.HasValidPayloadSha256() ||
            !string.Equals(accepted.PayloadSha256,c.RegimePayloadSha256,StringComparison.Ordinal) || accepted.ResultId != c.RegimeResultEnvelope.ResultId ||
            !string.Equals(accepted.ResultType,c.RegimeResultEnvelope.ResultType,StringComparison.Ordinal) || accepted.SchemaVersion != c.RegimeResultEnvelope.SchemaVersion ||
            !accepted.HasSameContent(c.RegimeResultEnvelope))
            errors.Add(new("Assessment request conflicts with its frozen workflow, trigger, profile or accepted regime."));
        return errors;
    }

    // Command-local adapters intentionally do not register global IValidationRules<T> services.
    sealed class ParameterRules : BaseValidationRules
    {
        public ValidationError[] Execute(MarketConditionAssessmentParameterSet value) => Validate(value, new Rules());
        sealed class Rules : AbstractValidator<MarketConditionAssessmentParameterSet>
        {
            public Rules() => RuleFor(x => x).Must(x => x.IsValid()).WithMessage("Invalid market assessment profile or unsupported source binding.");
        }
    }

    sealed class WorkflowRules : BaseValidationRules
    {
        public ValidationError[] Execute(IntrinsicTimeStrategyWorkflowView value) => Validate(value, new Rules());
        sealed class Rules : AbstractValidator<IntrinsicTimeStrategyWorkflowView>
        {
            public Rules()
            {
                RuleFor(x => x.AssessmentBinding).NotNull();
                RuleFor(x => x.RegimeDiscovery).NotNull();
                RuleFor(x => x.RegimeDiscoveryParameterSet).NotNull();
                RuleFor(x => x.TriggerEvent).NotNull();
                RuleFor(x => x.TriggerEvent.EntityId).NotNull().When(x => x.TriggerEvent is not null);
                RuleFor(x => x.AssessmentBinding).Must(binding => binding.ModeVersion == 1 &&
                    binding.Parameters is { } p && p.IsValid() && string.Equals(binding.PayloadSha256,MarketConditionAssessmentHash.Parameters(p),StringComparison.Ordinal))
                    .When(x => x.AssessmentBinding is not null).WithMessage("Invalid frozen assessment mode or parameter hash.");
            }
        }
    }

    sealed class TriggerRules : BaseValidationRules
    {
        public ValidationError[] Execute(FuturesItiSignalGeneratedEvent value) => Validate(value, new Rules());
        sealed class Rules : AbstractValidator<FuturesItiSignalGeneratedEvent>
        {
            public Rules()
            {
                RuleFor(x => x.EntityId).NotNull().DependentRules(() =>
                {
                    RuleFor(x => x.EntityId.ContractId).NotEmpty();
                    RuleFor(x => x.EntityId.TimePeriod).Must(MarketConditionAssessmentParameterSet.IsHorizon);
                });
            }
        }
    }
}
