namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs while streaming futures option quote data.
/// </summary>
/// <remarks>This exception typically indicates issues such as connectivity failures, data corruption, or other
/// problems that interrupt the streaming of futures option quote data. Catch this exception to handle interruptions or
/// failures in the data streaming process.</remarks>
public class StopFuturesOptionQuoteDataStreamingException : ApplicationException
{
    public StopFuturesOptionQuoteDataStreamingException(string errorMessage):base(errorMessage)
    {
    }

    public StopFuturesOptionQuoteDataStreamingException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
