using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Exceptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class ValidationException : ApplicationException
    {
        public ValidationException() : this(string.Empty, "Validation failed") { }

        public ValidationException(string message) : this(string.Empty, message) { }

        public ValidationException(string message, Exception innerException) : base(message, innerException) { _validationSource = string.Empty; }
        public ValidationException(string validationSource, string errorMessage)
            : base(errorMessage)
        {
            _validationSource = validationSource;
        }

        private readonly string _validationSource;

        public string ValidationSource => _validationSource;
    }
}
