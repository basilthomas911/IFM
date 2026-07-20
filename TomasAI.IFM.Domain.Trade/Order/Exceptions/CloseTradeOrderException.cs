namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class CloseTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
