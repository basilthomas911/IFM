using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Validation
{
    public class ValidationException : ApplicationException
    {
        private ValidationError[] _validationErrors;

        public ValidationError[] ValidationErrors => _validationErrors;

        public ValidationException(ValidationError[] validationErrors)
        {
            _validationErrors = validationErrors;
        }
    }
}
