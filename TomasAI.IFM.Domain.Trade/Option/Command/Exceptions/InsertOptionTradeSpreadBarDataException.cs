namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class InsertOptionTradeSpreadBarDataException(string errorMessage) : ApplicationException(errorMessage)
{
}
