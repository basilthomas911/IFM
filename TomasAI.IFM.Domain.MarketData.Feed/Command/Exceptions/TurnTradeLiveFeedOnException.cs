using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

public class TurnTradeLiveFeedOnException : Exception
{
    public TurnTradeLiveFeedOnException() { }

    public TurnTradeLiveFeedOnException(string message) : base(message) { }

    public TurnTradeLiveFeedOnException(string message, Exception innerException) : base(message, innerException) { }
}
