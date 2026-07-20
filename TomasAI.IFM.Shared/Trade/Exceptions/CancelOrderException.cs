using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.Exceptions
{
    public class CancelOrderException : ApplicationException
    {
        public CancelOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
