namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs during the insertion of futures option quote data.
/// </summary>
/// <remarks>Use this exception to indicate failures related to processing or persisting futures option quote
/// data, such as data validation errors or database issues. The exception message provides details about the specific
/// error encountered. This exception is intended for scenarios where a more specific error type is needed than
/// ApplicationException.</remarks>
public class InsertFuturesOptionQuoteDataException : ApplicationException
{
    public InsertFuturesOptionQuoteDataException(string errorMessage):base(errorMessage)
    {
    }

    public InsertFuturesOptionQuoteDataException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
