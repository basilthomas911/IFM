using FluentValidation;

namespace TomasAI.IFM.Shared.Validation;

public class BaseValidationRules
{
    public ValidationError[] Validate<TData>(TData dataSource, AbstractValidator<TData> validator)
    {
        ValidationError[] errors = [];
        var validationResult = validator.Validate(dataSource);
        if (!validationResult.IsValid)
        {
            errors = [.. validationResult.Errors.Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))];
        }
        return errors;
    }
}
