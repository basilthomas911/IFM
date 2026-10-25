using System.Text;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.CommandPipeline;

public class BaseValidationDecorator<TState> : IValidationCommandDecorator<TState> where TState : IBoundedContextState
{
    public void Validate(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var validatorType = typeof(IValidate<>).MakeGenericType(command.GetType());
        if (validatorType != null)
        {
            dynamic validator = this;
            validator.ValidateCommand((dynamic)command);
        }
    }

    protected static void ThrowCommandValidationExceptionOnErrors(int errorCode, ICollection<ValidationError> errors)
    {
        if (errors != null && errors.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var e in errors)
                sb.AppendLine(e.ErrorMessage);
            throw new CommandValidationException(errorCode, $"{sb}");
        }

    }

    /// <summary>
    /// validate command id
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="commandId"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
    protected static void ValidateCommandId(List<ValidationError> validationErrors, Guid commandId, string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (commandId == Guid.Empty)
            validationErrors.Add(new ValidationError($"{commandName}.CommandId is empty"));
    }
}
