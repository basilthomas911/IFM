using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

internal class RemoveTradeLiveFeedException : ApplicationException
{
    public RemoveTradeLiveFeedException() { }

    public RemoveTradeLiveFeedException(string message) : base(message) { }

    public RemoveTradeLiveFeedException(string message, Exception innerException) : base(message, innerException) { }
}
