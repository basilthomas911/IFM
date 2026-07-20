namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class DeleteOptionTradeException(string errorMessage) : ApplicationException(errorMessage)
{
}
