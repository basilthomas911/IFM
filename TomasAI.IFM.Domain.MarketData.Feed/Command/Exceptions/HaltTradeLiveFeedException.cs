namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

public class HaltTradeLiveFeedException : Exception
{
    public HaltTradeLiveFeedException() { }

    public HaltTradeLiveFeedException(string message) : base(message) { }

    public HaltTradeLiveFeedException(string message, Exception innerException) : base(message, innerException) { }
}
