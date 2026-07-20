using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public class QueryException : ApplicationException
    {
        private readonly int _errorCode;

        public int ErrorCode => _errorCode;

        public QueryException(int errorCode, string errorMessage)
            :base(errorMessage)
        {
            _errorCode = errorCode;
        }

        public QueryException(int errorCode, string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
            _errorCode = errorCode;
        }
    }
}
