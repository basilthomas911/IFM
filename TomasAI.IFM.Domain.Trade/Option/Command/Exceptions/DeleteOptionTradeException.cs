namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class DeleteOptionTradeException(string errorMessage) : ApplicationException(errorMessage)
{
}
