using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public class CommandException : ApplicationException
    {
        private readonly int _errorCode;

        public int ErrorCode => _errorCode;

        public CommandException(int errorCode, string errorMessage)
            :base(errorMessage)
        {
            _errorCode = errorCode;
        }

        public CommandException(int errorCode, string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
            _errorCode = errorCode;
        }
    }
}
