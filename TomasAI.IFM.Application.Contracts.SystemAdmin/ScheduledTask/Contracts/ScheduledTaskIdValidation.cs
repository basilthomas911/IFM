using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Shared.Validation;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Accumulates intrinsic scheduled-task aggregate identity errors.</summary>
public static class ScheduledTaskIdValidation
{

    extension<TErrors>(TErrors errors)
        where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Requires a nonempty identity without reading aggregate state.</summary>
        public TErrors ValidateScheduledTaskId(ScheduledTaskId identity, string commandName)
        {
            ArgumentNullException.ThrowIfNull(errors);
            if (!identity.IsValid) errors.Add(new($"{commandName}: scheduled-task aggregate identity is required."));
            return errors;
        }
    }
}
