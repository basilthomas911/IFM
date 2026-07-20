using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Application.ValidationRules
{
    public class BaseValidationRules
    {
        public ValidationError[] Validate<TData>( TData dataSource, IValidator<TData> validator)
        {
            var errors = default(ValidationError[]);
            var validationResult = validator.Validate(dataSource);
            if (!validationResult.IsValid)
            {
                errors = validationResult
                    .Errors.Select(e => new ValidationError(e.ErrorCode, e.ErrorMessage))
                    .ToArray();
            }
            return errors;
        }
    }
}
