namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class InsertOptionTradeSpreadDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
