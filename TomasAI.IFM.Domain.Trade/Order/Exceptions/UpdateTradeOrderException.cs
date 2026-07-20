namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class UpdateTradeOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
