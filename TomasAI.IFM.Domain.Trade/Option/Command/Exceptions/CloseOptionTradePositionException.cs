namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class CloseOptionTradePositionException(string errorMessage) : ApplicationException(errorMessage)
{
}
