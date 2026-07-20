using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public class EventException : ApplicationException
    {
        private readonly int _errorCode;

        public int ErrorCode => _errorCode;

        public EventException(int errorCode, string errorMessage)
            :base(errorMessage)
        {
            _errorCode = errorCode;
        }

        public EventException(int errorCode, string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
            _errorCode = errorCode;
        }
    }
}
