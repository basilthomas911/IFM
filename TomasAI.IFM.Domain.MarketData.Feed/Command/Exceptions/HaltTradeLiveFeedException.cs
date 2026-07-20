namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Exceptions;

internal class HaltTradeLiveFeedException : ApplicationException
{
    public HaltTradeLiveFeedException() { }

    public HaltTradeLiveFeedException(string message) : base(message) { }

    public HaltTradeLiveFeedException(string message, Exception innerException) : base(message, innerException) { }
}
