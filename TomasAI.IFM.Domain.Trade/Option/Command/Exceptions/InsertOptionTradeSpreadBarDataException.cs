namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class InsertOptionTradeSpreadBarDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
