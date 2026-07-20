namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Exceptions;

public class PlaceOptionTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
