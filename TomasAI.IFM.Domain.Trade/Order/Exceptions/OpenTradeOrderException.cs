namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class OpenTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
