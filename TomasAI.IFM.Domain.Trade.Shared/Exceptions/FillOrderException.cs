using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Exceptions
{
    public class FillOrderException : ApplicationException
    {
        public FillOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
