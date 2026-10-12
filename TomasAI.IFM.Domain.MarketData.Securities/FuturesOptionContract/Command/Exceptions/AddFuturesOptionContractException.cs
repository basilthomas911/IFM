using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions
{
    public class AddFuturesOptionContractException : Exception
    {
        public AddFuturesOptionContractException(string errorMessage) : base(errorMessage)
        {
        }

        public AddFuturesOptionContractException() : base()
        {
        }

        public AddFuturesOptionContractException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
