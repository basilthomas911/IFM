namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs while starting the streaming of futures option quote
/// data.
/// </summary>
/// <remarks>This exception is intended to signal issues encountered during the initialization or startup phase of
/// futures option quote data streaming. It can be used to distinguish streaming startup errors from other types of
/// application exceptions, allowing for more precise error handling in market data feed scenarios.</remarks>
public class StartFuturesOptionQuoteDataStreamingException : ApplicationException
{
    public StartFuturesOptionQuoteDataStreamingException(string errorMessage):base(errorMessage)
    {
    }

    public StartFuturesOptionQuoteDataStreamingException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
