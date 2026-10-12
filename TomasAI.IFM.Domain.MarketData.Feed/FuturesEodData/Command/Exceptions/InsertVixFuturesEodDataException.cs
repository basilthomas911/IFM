namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Exceptions;

public class InsertVixFuturesEodDataException : Exception
{
    public InsertVixFuturesEodDataException() { }

    public InsertVixFuturesEodDataException(string errorMessage) : base(errorMessage)
    {
    }

    public InsertVixFuturesEodDataException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
