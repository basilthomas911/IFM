namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class ExecuteCancelOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
