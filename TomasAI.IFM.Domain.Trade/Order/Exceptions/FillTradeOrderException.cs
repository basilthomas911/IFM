namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class FillTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
