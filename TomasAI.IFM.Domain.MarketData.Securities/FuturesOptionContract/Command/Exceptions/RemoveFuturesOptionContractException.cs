using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;

public class RemoveFuturesOptionContractException : ApplicationException
{
    public RemoveFuturesOptionContractException(string errorMessage) : base(errorMessage)
    {
    }

    public RemoveFuturesOptionContractException() : base()
    {
    }

    protected RemoveFuturesOptionContractException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
    {
    }

    public RemoveFuturesOptionContractException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
