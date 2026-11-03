using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Application.CommandPipeline;

/// <summary>Validation helpers shared by all actor domains.</summary>
public static class ValidationErrorsExtension
{

    extension(List<ValidationError> validationErrors)
    {
        /// <summary>Captures expected validation exceptions as aggregate errors.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> CaptureCommandValidation(Action validation)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            ArgumentNullException.ThrowIfNull(validation);
            try { validation(); }
            catch (CommandValidationException exception) { validationErrors.Add(new(exception.Message)); }
            catch (ArgumentException exception) { validationErrors.Add(new(exception.Message)); }
            return validationErrors;
        }

        /// <summary>Applies structural checks common to all actor entity identifiers.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The validation boundary turns entity ID construction failures into explicit validation errors.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ValidateEntityId(IActorEntityId? entityId, string commandName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            if (entityId is null)
            {
                validationErrors.Add(new($"{commandName}.EntityId is null"));
                return validationErrors;
            }
            try
            {
                if (string.IsNullOrWhiteSpace(entityId.Format()))
                    validationErrors.Add(new($"{commandName}.EntityId format is empty"));
            }
            catch (Exception exception)
            {
                validationErrors.Add(new($"{commandName}.EntityId is invalid: {exception.Message}"));
            }
            return validationErrors;
        }

        /// <summary>Validates the concrete entity identifier of a non-generic command reference.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ValidateEntityId(ICommand command, string commandName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            ArgumentNullException.ThrowIfNull(command);
            var property = command.GetType().GetProperty("EntityId");
            if (property?.GetValue(command) is not IActorEntityId entityId)
            {
                validationErrors.Add(new($"{commandName}.EntityId is missing or invalid"));
                return validationErrors;
            }
            return validationErrors.ValidateEntityId(entityId, commandName);
        }
    }

    /// <param name="errors">
    /// Errors to inspect.
    /// </param>
    extension(List<ValidationError> errors)
    {
        /// <summary>Throws when the aggregate contains validation errors.</summary>
        ///
        /// <param name="errorCode">Code for the validation exception.</param>
        /// <returns>The original list when no errors exist.</returns>
        /// <exception cref="CommandValidationException">The list contains an error.</exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ThrowCommandValidationExceptionOnAnyError(int errorCode)
        {
            if (errors != null && errors.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var error in errors)
                    sb.AppendLine(error.ErrorMessage);
                throw new CommandValidationException(errorCode, $"{sb}");
            }
            return errors!;
        }
    }

    extension(List<ValidationError> validationErrors)
    {
        /// <summary>Validates a command identifier.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ValidateCommandId(Guid commandId, string commandName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            if (commandId == Guid.Empty)
                validationErrors.Add(new($"{commandName}.CommandId is empty"));
            return validationErrors;
        }

        /// <summary>Validates a named DateOnly field without embedding domain field names in the shared API.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ValidateDateOnly(DateOnly value, string commandName, string fieldName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            if (value == DateOnly.MinValue || value == DateOnly.MaxValue)
                validationErrors.Add(new($"{commandName}.{fieldName} is invalid"));
            return validationErrors;
        }
    }

    extension<TErrors>(TErrors validationErrors)
        where TErrors : class, ICollection<ValidationError>
    {
        /// <summary>Validates a named DateTime field without embedding domain field names in the shared API.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public TErrors ValidateDateTime(DateTime value, string commandName, string fieldName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            if (value == DateTime.MinValue || value == DateTime.MaxValue)
                validationErrors.Add(new($"{commandName}.{fieldName} is invalid"));
            return validationErrors;
        }
    }
}
