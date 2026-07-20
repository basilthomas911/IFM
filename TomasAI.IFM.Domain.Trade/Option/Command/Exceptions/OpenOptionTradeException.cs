namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class OpenOptionTradeException(string errorMessage) : ApplicationException(errorMessage)
{
}
