namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class CancelTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
