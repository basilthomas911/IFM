using System.Text.Json;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Validation;

/// <summary>Accumulates deterministic parameter command errors before configuration access.</summary>
public static class ParameterCommandValidation
{
    /// <summary>Checks parameter mutation inputs; schema-specific readiness remains in the mapped handler.</summary>
    public static List<ValidationError> ValidateParameterCommand(this List<ValidationError> errors, IParameterSetMutation command)
    {
        if (command.ExpectedRevision < 0) errors.Add(new("ParameterSet.ExpectedRevision cannot be negative."));
        if (command is CreateParameterSetCommand or SaveParameterDraftCommand)
        {
            if (string.IsNullOrWhiteSpace(command.ComponentCode)) errors.Add(new("ParameterSet.ComponentCode is required."));
            if (command.SchemaVersion <= 0) errors.Add(new("ParameterSet.SchemaVersion must be positive."));
            using var payload = ValidateJson(errors, command.PayloadJson, "ParameterSet.PayloadJson");
        }
        if (command is CreateParameterSetCommand or RenameParameterSetCommand && string.IsNullOrWhiteSpace(command.Name))
            errors.Add(new("ParameterSet.Name is required."));
        if (command is PublishParameterVersionCommand or RetireParameterVersionCommand && command.Version <= 0)
            errors.Add(new("ParameterSet.Version must be positive."));
        if (command.Description is null) errors.Add(new("ParameterSet.Description cannot be null."));
        return errors;
    }

    /// <summary>Checks an assignment scope and reference against the deterministic scope identity.</summary>
    public static List<ValidationError> ValidateParameterCommand(this List<ValidationError> errors, AssignParameterVersionCommand command)
    {
        ValidateAssignment(errors, command.EntityId, command.Scope, command.ExpectedRevision);
        if (command.Reference is null) errors.Add(new("ParameterAssignment.Reference is required."));
        else if (command.Reference.SetId == Guid.Empty || command.Reference.Version <= 0 || string.IsNullOrWhiteSpace(command.Reference.ComponentCode))
            errors.Add(new("ParameterAssignment.Reference requires a set, version and component."));
        return errors;
    }

    /// <summary>Checks the disabled assignment's scope and expected revision.</summary>
    public static List<ValidationError> ValidateParameterCommand(this List<ValidationError> errors, DisableParameterAssignmentCommand command)
    {
        ValidateAssignment(errors, command.EntityId, command.Scope, command.ExpectedRevision);
        return errors;
    }

    /// <summary>Checks startup run identity and its operation correlation.</summary>
    public static List<ValidationError> ValidateParameterCommand(this List<ValidationError> errors, IParameterStartupMutation command)
    {
        if (command.RunId == Guid.Empty) errors.Add(new("ParameterStartup.RunId is required."));
        if (command is ApplySignalStartupPlanCommand && command.CommandId != command.RunId)
            errors.Add(new("ParameterStartup.CommandId must match RunId when applying a plan."));
        if (command.ExpectedFingerprint is null) errors.Add(new("ParameterStartup.ExpectedFingerprint cannot be null."));
        if (command is RecordSignalStartupReportCommand { Report: null }) errors.Add(new("ParameterStartup.Report is required."));
        return errors;
    }

    /// <summary>Validates scope fields before computing their exact assignment identity.</summary>
    private static void ValidateAssignment(List<ValidationError> errors, ParameterAssignmentEntityId id, ParameterAssignmentScope? scope, long revision)
    {
        if (revision < 0) errors.Add(new("ParameterAssignment.ExpectedRevision cannot be negative."));
        if (scope is null) { errors.Add(new("ParameterAssignment.Scope is required.")); return; }
        ParameterAssignmentScope? expected = scope.ComponentCode switch
        {
            IronCondorMarketSelectionParameterModel.ComponentCode => OptionSpreadStrategyParameterScopeModel.IronCondor(),
            VerticalSpreadMarketSelectionParameterModel.ComponentCode => OptionSpreadStrategyParameterScopeModel.VerticalSpread(),
            _ => null
        };
        if (scope.ComponentCode == RegimeDiscoveryParameterModel.ComponentCode)
        {
            using var json = ValidateJson(errors, scope.ScopeJson, "ParameterAssignment.ScopeJson");
            if (json is not null && json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("TargetHorizon", out var horizonValue) && horizonValue.ValueKind == JsonValueKind.String &&
                Enum.TryParse<TimeFrameType>(horizonValue.GetString(), out var horizon) &&
                horizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly &&
                scope.ConsumerId == TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.IntrinsicTimeStrategyWorkflowDefinition.Id)
                expected = WorkflowParameterScopeModel.Create(scope.ConsumerId, horizon);
        }
        if (expected is null || expected != scope) errors.Add(new("ParameterAssignment.Scope is unsupported or not canonical."));
        else if (id.AssignmentId != ParameterAssignmentPolicyModel.AssignmentId(expected))
            errors.Add(new("ParameterAssignment.EntityId must match the scope assignment identity."));
    }

    /// <summary>Parses structured JSON and reports ordinary malformed data without propagating parsing exceptions.</summary>
    private static JsonDocument? ValidateJson(List<ValidationError> errors, string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) { errors.Add(new($"{name} is required.")); return null; }
        try { return JsonDocument.Parse(json); }
        catch (JsonException exception) { errors.Add(new($"{name} is invalid: {exception.Message}")); return null; }
    }
}
