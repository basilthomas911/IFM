namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class CloseOptionTradeException : ApplicationException
{
    public CloseOptionTradeException(string errorMessage) : base(errorMessage)
    {
    }
}
