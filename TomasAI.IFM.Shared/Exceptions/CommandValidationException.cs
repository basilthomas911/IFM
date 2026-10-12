using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class CommandValidationException : ApplicationException
{
        public CommandValidationException() : this(0, "CommandValidationException") { }

        public CommandValidationException(string message) : this(0, message) { }

        public CommandValidationException(string message, Exception innerException) : this(0, message, innerException) { }

    public CommandValidationException(int errorCode, string errorMessage)
        : base(errorMessage)
    {
        _errorCode = errorCode;
    }

    public CommandValidationException(int errorCode, string errorMessage, Exception innerException)
        : base(errorMessage, innerException)
    {
        _errorCode = errorCode;
    }

    private readonly int _errorCode;

    public int ErrorCode => _errorCode;
}
