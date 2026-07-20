namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Exceptions;

public class InsertFuturesClosingPriceException : ApplicationException
{
    public InsertFuturesClosingPriceException(string errorMessage):base(errorMessage)
    {
    }

    public InsertFuturesClosingPriceException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
