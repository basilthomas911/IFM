using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Exceptions
{
    public class PlaceOrderException : ApplicationException
    {
        public PlaceOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
