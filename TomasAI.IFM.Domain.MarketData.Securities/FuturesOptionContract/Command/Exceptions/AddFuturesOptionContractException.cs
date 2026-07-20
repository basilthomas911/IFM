using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions
{
    public class AddFuturesOptionContractException : ApplicationException
    {
        public AddFuturesOptionContractException(string errorMessage) : base(errorMessage)
        {
        }

        public AddFuturesOptionContractException() : base()
        {
        }

        protected AddFuturesOptionContractException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }

        public AddFuturesOptionContractException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
