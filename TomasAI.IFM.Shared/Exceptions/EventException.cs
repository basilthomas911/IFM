using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class EventException : ApplicationException
    {
        public EventException() : this(0, "EventException") { }

        public EventException(string message) : this(0, message) { }

        public EventException(string message, Exception innerException) : this(0, message, innerException) { }

        public EventException(int errorCode, string errorMessage)
            : base(errorMessage)
        {
            _errorCode = errorCode;
        }

        public EventException(int errorCode, string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
            _errorCode = errorCode;
        }

        private readonly int _errorCode;

        public int ErrorCode => _errorCode;
    }
}
