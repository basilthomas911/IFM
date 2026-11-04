using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Validation;

/// <summary>Validates inputs for this aggregate owner before loading state.</summary>
public static class ParameterAssignmentValidation
{
    extension<TErrors>(TErrors errors) where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Checks an assignment scope and reference against the deterministic scope identity.</summary>
        public TErrors ValidateParameterCommand(AssignParameterVersionCommand command)
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
        public TErrors ValidateParameterCommand(DisableParameterAssignmentCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(errors);
            ValidateAssignment(errors, command.EntityId, command.Scope, command.ExpectedRevision);
            return errors;
        }
    }

    /// <summary>Validates scope fields before computing their exact assignment identity.</summary>
    private static void ValidateAssignment(ICollection<ValidationError> errors, ParameterAssignmentEntityId id, ParameterAssignmentScope? scope, long revision)
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
            var expectedId = ParameterAssignmentPolicyModel.AssignmentId(scope);
            if (expectedId != id.AssignmentId) errors.Add(new("ParameterAssignment.Scope identity does not match"));
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException)
        { errors.Add(new(error.Message)); }

    }
}
