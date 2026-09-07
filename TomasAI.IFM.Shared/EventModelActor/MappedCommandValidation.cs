using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Common exact-type validation for Command and Function actor ingress.</summary>
internal static class MappedCommandValidation
{
    internal static void Validate(
        string actorName,
        ICommand command,
        IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> validationMap)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(validationMap);
        if (!validationMap.TryGetValue(command.GetType(), out var validator))
            throw new InvalidOperationException(
                $"Unable to validate {actorName} commands from message: {command.Subject}");

        var errors = validator(command)
            ?? throw new InvalidOperationException(
                $"Validator for {command.GetType().Name} returned no error collection.");
        if (errors.Count > 0)
            throw new CommandValidationException(command.ErrorCode,
                string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage))
                + Environment.NewLine);
    }
}
