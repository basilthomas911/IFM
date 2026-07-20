namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class InsertOptionTradeSpreadDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
