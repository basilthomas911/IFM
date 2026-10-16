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
    public static TErrors ValidateParameterCommand<TErrors>(this TErrors errors, IParameterSetMutation command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (command.ExpectedRevision < 0) errors.Add(new("ParameterSet.ExpectedRevision cannot be negative."));
        if (command is CreateParameterSetCommand or SaveParameterDraftCommand)
        {
            if (string.IsNullOrWhiteSpace(command.ComponentCode)) errors.Add(new("ParameterSet.ComponentCode is required."));
            if (command.SchemaVersion <= 0) errors.Add(new("ParameterSet.SchemaVersion must be positive."));
            if(command.Parameters is {} typed)
            {
                foreach(var issue in TypedParameterDraftModel.ValidateShape(command.ComponentCode,command.SchemaVersion,typed))
                    errors.Add(new(issue.Message));
            }
            else errors.Add(new("PARAM.TYPED_VALUES_REQUIRED"));
            if(!string.IsNullOrEmpty(command.PayloadJson))errors.Add(new("PARAM.JSON_INPUT_RETIRED"));
        }
        if (command is CreateParameterSetCommand or RenameParameterSetCommand && string.IsNullOrWhiteSpace(command.Name))
            errors.Add(new("ParameterSet.Name is required."));
        if (command is PublishParameterVersionCommand or RetireParameterVersionCommand && command.Version <= 0)
            errors.Add(new("ParameterSet.Version must be positive."));
        if (command.Description is null) errors.Add(new("ParameterSet.Description cannot be null."));
        return errors;
    }

    /// <summary>Checks an assignment scope and reference against the deterministic scope identity.</summary>
    public static TErrors ValidateParameterCommand<TErrors>(this TErrors errors, AssignParameterVersionCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        ValidateAssignment(errors, command.EntityId, command.Scope, command.ExpectedRevision);
        if (command.Reference is null) errors.Add(new("ParameterAssignment.Reference is required."));
        else if (command.Reference.SetId == Guid.Empty || command.Reference.Version <= 0 || string.IsNullOrWhiteSpace(command.Reference.ComponentCode))
            errors.Add(new("ParameterAssignment.Reference requires a set, version and component."));
        return errors;
    }

    /// <summary>Checks the disabled assignment's scope and expected revision.</summary>
    public static TErrors ValidateParameterCommand<TErrors>(this TErrors errors, DisableParameterAssignmentCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        ValidateAssignment(errors, command.EntityId, command.Scope, command.ExpectedRevision);
        return errors;
    }

    /// <summary>Checks startup run identity and its operation correlation.</summary>
    public static TErrors ValidateParameterCommand<TErrors>(this TErrors errors, IParameterStartupMutation command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (command.RunId == Guid.Empty) errors.Add(new("ParameterStartup.RunId is required."));
        if (command is ApplySignalStartupPlanCommand && command.CommandId != command.RunId)
            errors.Add(new("ParameterStartup.CommandId must match RunId when applying a plan."));
        if (command.ExpectedFingerprint is null) errors.Add(new("ParameterStartup.ExpectedFingerprint cannot be null."));
        if (command is RecordSignalStartupReportCommand { Report: null }) errors.Add(new("ParameterStartup.Report is required."));
        return errors;
    }

    /// <summary>Validates scope fields before computing their exact assignment identity.</summary>
    private static void ValidateAssignment(ICollection<ValidationError> errors,ParameterAssignmentEntityId id,ParameterAssignmentScope? scope,long revision)
    {
        if (revision < 0) errors.Add(new("ParameterAssignment.ExpectedRevision cannot be negative."));
        if (scope is null) { errors.Add(new("ParameterAssignment.Scope is required.")); return; }
        ParameterAssignmentScope? expected = scope.ComponentCode switch
        {
            IronCondorMarketSelectionParameterModel.ComponentCode => OptionSpreadStrategyParameterScopeModel.IronCondor(),
            VerticalSpreadMarketSelectionParameterModel.ComponentCode => OptionSpreadStrategyParameterScopeModel.VerticalSpread(),
            _ => null
        };
        try
        {
            var expectedId=ParameterAssignmentPolicyModel.AssignmentId(scope);
            if(expectedId!=id.AssignmentId)errors.Add(new("ParameterAssignment.Scope identity does not match"));
        }
        catch(Exception error) when(error is ArgumentException or InvalidDataException)
        {errors.Add(new(error.Message));}

    }

    /// <summary>Parses structured JSON and reports ordinary malformed data without propagating parsing exceptions.</summary>
    private static JsonDocument? ValidateJson(ICollection<ValidationError> errors,string? json,string name)
    {
        if (string.IsNullOrWhiteSpace(json)) { errors.Add(new($"{name} is required.")); return null; }
        try { return JsonDocument.Parse(json); }
        catch (JsonException exception) { errors.Add(new($"{name} is invalid: {exception.Message}")); return null; }
    }
}
