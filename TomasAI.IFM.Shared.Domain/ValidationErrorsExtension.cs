using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.Domain;

/// <summary>Validation helpers shared by all actor domains.</summary>
public static class ValidationErrorsExtension
{
    /// <summary>Captures expected validation exceptions as aggregate errors.</summary>
    public static List<ValidationError> CaptureCommandValidation(this List<ValidationError> validationErrors, Action validation)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        ArgumentNullException.ThrowIfNull(validation);
        try { validation(); }
        catch (CommandValidationException exception) { validationErrors.Add(new(exception.Message)); }
        catch (ArgumentException exception) { validationErrors.Add(new(exception.Message)); }
        return validationErrors;
    }

    /// <summary>Applies structural checks common to all actor entity identifiers.</summary>
    public static List<ValidationError> ValidateEntityId(this List<ValidationError> validationErrors, IActorEntityId? entityId, string commandName)
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
    public static List<ValidationError> ValidateEntityId(this List<ValidationError> validationErrors, ICommand command, string commandName)
    {
        ArgumentNullException.ThrowIfNull(command);
        var property = command.GetType().GetProperty("EntityId");
        if (property?.GetValue(command) is not IActorEntityId entityId)
        {
            validationErrors.Add(new($"{commandName}.EntityId is missing or invalid"));
            return validationErrors;
        }
        return validationErrors.ValidateEntityId(entityId, commandName);
    }

    /// <summary>Throws when the aggregate contains validation errors.</summary>
    /// <param name="errors">Errors to inspect.</param>
    /// <param name="errorCode">Code for the validation exception.</param>
    /// <returns>The original list when no errors exist.</returns>
    /// <exception cref="CommandValidationException">The list contains an error.</exception>
    public static List<ValidationError> ThrowCommandValidationExceptionOnAnyError(this List<ValidationError> errors, int errorCode)
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

    /// <summary>Validates a command identifier.</summary>
    public static List<ValidationError> ValidateCommandId(this List<ValidationError> validationErrors, Guid commandId, string commandName)
    {
        if (commandId == Guid.Empty)
            validationErrors.Add(new($"{commandName}.CommandId is empty"));
        return validationErrors;
    }

    /// <summary>Validates a named DateOnly field without embedding domain field names in the shared API.</summary>
    public static List<ValidationError> ValidateDateOnly(this List<ValidationError> validationErrors, DateOnly value, string commandName, string fieldName)
    {
        if (value == DateOnly.MinValue || value == DateOnly.MaxValue)
            validationErrors.Add(new($"{commandName}.{fieldName} is invalid"));
        return validationErrors;
    }

    /// <summary>Validates a named DateTime field without embedding domain field names in the shared API.</summary>
    public static List<ValidationError> ValidateDateTime(this List<ValidationError> validationErrors, DateTime value, string commandName, string fieldName)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            validationErrors.Add(new($"{commandName}.{fieldName} is invalid"));
        return validationErrors;
    }
}
