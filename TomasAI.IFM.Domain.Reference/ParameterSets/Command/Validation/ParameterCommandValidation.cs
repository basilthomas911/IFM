using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Validation;

/// <summary>Validates inputs for this aggregate owner before loading state.</summary>
public static class ParameterCommandValidation
{
    extension<TErrors>(TErrors errors) where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Checks parameter mutation inputs; schema-specific readiness remains in the mapped handler.</summary>
        public TErrors ValidateParameterCommand(IParameterSetMutation command)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(errors);
            if (command.ExpectedRevision < 0) errors.Add(new("ParameterSet.ExpectedRevision cannot be negative."));
            if (command is CreateParameterSetCommand or SaveParameterDraftCommand)
            {
                if (string.IsNullOrWhiteSpace(command.ComponentCode)) errors.Add(new("ParameterSet.ComponentCode is required."));
                if (command.SchemaVersion <= 0) errors.Add(new("ParameterSet.SchemaVersion must be positive."));
                if (command.Parameters is { } typed)
                {
                    foreach (var issue in TypedParameterDraftModel.ValidateShape(command.ComponentCode, command.SchemaVersion, typed))
                        errors.Add(new(issue.Message));
                }
                else errors.Add(new("PARAM.TYPED_VALUES_REQUIRED"));
                if (!string.IsNullOrEmpty(command.PayloadJson)) errors.Add(new("PARAM.JSON_INPUT_RETIRED"));
            }
            if (command is CreateParameterSetCommand or RenameParameterSetCommand && string.IsNullOrWhiteSpace(command.Name))
                errors.Add(new("ParameterSet.Name is required."));
            if (command is PublishParameterVersionCommand or RetireParameterVersionCommand && command.Version <= 0)
                errors.Add(new("ParameterSet.Version must be positive."));
            if (command.Description is null) errors.Add(new("ParameterSet.Description cannot be null."));
            return errors;
        }
    }
}
