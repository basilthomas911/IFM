using FluentValidation;

namespace TomasAI.IFM.Shared.Validation;

public static class IDataValidationExtension
{

    extension(IDataValidation dataValidation)
    {
        public ValidationError[] Validate<TData>(IValidator<TData> validator)
            => dataValidation.Validate((TData)dataValidation, validator);

        public ValidationError[] Validate<TData>(TData dataSource, IValidator<TData> validator)
        {
            ArgumentNullException.ThrowIfNull(validator);
            ValidationError[] errors = [];
            var validationResult = validator.Validate(dataSource);
            if (!validationResult.IsValid)
                errors = [.. validationResult.Errors.Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))];
            return errors;
        }
    }
}
