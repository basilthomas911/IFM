using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;

public class ChangeFuturesOptionContractException : ApplicationException
{
    public ChangeFuturesOptionContractException(string errorMessage) : base(errorMessage)
    {
    }

    public ChangeFuturesOptionContractException() : base()
    {
    }

    public ChangeFuturesOptionContractException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
