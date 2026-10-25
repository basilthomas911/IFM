using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Validation
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class ValidationException : ApplicationException
    {
        public ValidationException() : this([]) { }

        public ValidationException(string message) : base(message) { _validationErrors = []; }

        public ValidationException(string message, Exception innerException) : base(message, innerException) { _validationErrors = []; }

        public ValidationException(ValidationError[] validationErrors)
        {
            _validationErrors = validationErrors;
        }

        private readonly ValidationError[] _validationErrors;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
        public ValidationError[] ValidationErrors => _validationErrors;
    }
}
