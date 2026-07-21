namespace TomasAI.IFM.Domain.Trade.Option.Command.Exceptions;

public class PlaceOptionTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
