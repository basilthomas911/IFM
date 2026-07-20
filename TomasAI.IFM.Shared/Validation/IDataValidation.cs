using FluentValidation;

namespace TomasAI.IFM.Shared.Validation;

public interface IDataValidation 
{
}

public static class IDataValidationExtension
{
    public static ValidationError[] Validate<TData>(this IDataValidation dataValidation, IValidator<TData> validator)
        => dataValidation.Validate((TData)dataValidation, validator);

    public static ValidationError[] Validate<TData>(this IDataValidation dataValidation, TData dataSource, IValidator<TData> validator)
    {
        ValidationError[] errors = [];
        var validationResult = validator.Validate(dataSource);
        if (!validationResult.IsValid)
            errors = [.. validationResult.Errors.Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))];
        return errors;
    }
}
