using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public class ConcurrencyException : ApplicationException
    {
       

        public ConcurrencyException(string errorMessage) : base(errorMessage)
        {
        }

        public ConcurrencyException(string errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
}
