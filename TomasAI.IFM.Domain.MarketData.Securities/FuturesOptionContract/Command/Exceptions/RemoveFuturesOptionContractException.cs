using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;

public class RemoveFuturesOptionContractException : Exception
{
    public RemoveFuturesOptionContractException(string errorMessage) : base(errorMessage)
    {
    }

    public RemoveFuturesOptionContractException() : base()
    {
    }

    public RemoveFuturesOptionContractException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
