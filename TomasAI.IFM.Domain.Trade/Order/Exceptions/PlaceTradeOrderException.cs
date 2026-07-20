namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class PlaceTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
