using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.Exceptions
{
    public class UpdateOrderException : ApplicationException
    {
        public UpdateOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
