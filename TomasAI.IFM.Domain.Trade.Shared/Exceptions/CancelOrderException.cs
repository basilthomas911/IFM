using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Exceptions
{
    public class CancelOrderException : ApplicationException
    {
        public CancelOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
