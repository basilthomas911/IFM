using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Validation
{
    public class ValidationError
    {
        private readonly string _errorCode;
        private readonly string _errorMessage;

        public string ErrorCode => _errorCode;
        public string ErrorMessage => _errorMessage;

        public ValidationError(
            string errorCode,
            string errorMessage)
        {
            _errorCode = errorCode;
            _errorMessage = errorMessage;
        }

        public ValidationError(string errorMessage)
        {
            _errorCode = "999";
            _errorMessage = errorMessage;
        }

    }
}
