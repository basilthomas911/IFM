using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

internal class TurnTradeLiveFeedOffException : ApplicationException
{
    public TurnTradeLiveFeedOffException() { }

    public TurnTradeLiveFeedOffException(string message) : base(message) { }

    public TurnTradeLiveFeedOffException(string message, Exception innerException) : base(message, innerException) { }
}
