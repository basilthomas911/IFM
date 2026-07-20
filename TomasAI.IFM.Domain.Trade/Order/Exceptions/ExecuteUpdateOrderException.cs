namespace TomasAI.IFM.Domain.Trade.Order.Exceptions;

public class ExecuteUpdateOrderException(string errorMessage) : ApplicationException(errorMessage)
{
}
