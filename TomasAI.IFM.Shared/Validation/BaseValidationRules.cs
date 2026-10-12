using FluentValidation;

namespace TomasAI.IFM.Shared.Validation;

public class BaseValidationRules
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Preserve the instance member called by existing derived actor/validation consumers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "This instance member is part of the established command/event/validation contract consumed through instances.")]
    public ValidationError[] Validate<TData>(TData dataSource, AbstractValidator<TData> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ValidationError[] errors = [];
        var validationResult = validator.Validate(dataSource);
        if (!validationResult.IsValid)
        {
            errors = [.. validationResult.Errors.Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))];
        }
        return errors;
    }
}
