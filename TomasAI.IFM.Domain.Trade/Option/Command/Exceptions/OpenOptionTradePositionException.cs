namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class OpenOptionTradePositionException(string errorMessage) : ApplicationException(errorMessage)
{
}
