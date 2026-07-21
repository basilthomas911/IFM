namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class OpenOptionTradePositionException(string errorMessage) : ApplicationException(errorMessage)
{
}
