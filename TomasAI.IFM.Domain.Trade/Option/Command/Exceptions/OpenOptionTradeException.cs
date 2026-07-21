namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class OpenOptionTradeException(string errorMessage) : ApplicationException(errorMessage)
{
}
