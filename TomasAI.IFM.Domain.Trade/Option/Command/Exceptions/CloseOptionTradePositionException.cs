namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class CloseOptionTradePositionException(string errorMessage) : ApplicationException(errorMessage)
{
}
