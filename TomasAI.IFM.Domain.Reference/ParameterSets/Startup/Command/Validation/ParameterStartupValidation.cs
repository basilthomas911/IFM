using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Validation;

/// <summary>Validates inputs for this aggregate owner before loading state.</summary>
public static class ParameterStartupValidation
{
    extension<TErrors>(TErrors errors) where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Checks startup run identity and its operation correlation.</summary>
        public TErrors ValidateParameterCommand(IParameterStartupMutation command)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(errors);
            if (command.RunId == Guid.Empty) errors.Add(new("ParameterStartup.RunId is required."));
            if (command.EntityId.Id != command.RunId) errors.Add(new("ParameterStartup.EntityId must match RunId."));
            if (command is ApplySignalStartupPlanCommand && command.CommandId != command.RunId)
                errors.Add(new("ParameterStartup.CommandId must match RunId when applying a plan."));
            if (command.ExpectedFingerprint is null) errors.Add(new("ParameterStartup.ExpectedFingerprint cannot be null."));
            if (command is RecordSignalStartupReportCommand { Report: null }) errors.Add(new("ParameterStartup.Report is required."));
            return errors;
        }
    }
}
