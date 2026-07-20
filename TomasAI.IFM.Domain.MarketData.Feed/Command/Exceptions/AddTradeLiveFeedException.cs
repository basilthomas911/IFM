using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

internal class AddTradeLiveFeedException : ApplicationException
{
    public AddTradeLiveFeedException() { }

    public AddTradeLiveFeedException(string message) : base(message) { }

    public AddTradeLiveFeedException(string message, Exception innerException) : base(message, innerException) { }
}
