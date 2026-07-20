using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.Exceptions
{
    public class FillOrderException : ApplicationException
    {
        public FillOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
