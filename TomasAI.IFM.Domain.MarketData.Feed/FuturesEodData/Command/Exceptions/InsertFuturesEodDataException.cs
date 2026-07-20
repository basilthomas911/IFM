namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Exceptions;

public class InsertFuturesEodDataException : ApplicationException
{
    public InsertFuturesEodDataException(string errorMessage):base(errorMessage)
    {
    }

    public InsertFuturesEodDataException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
