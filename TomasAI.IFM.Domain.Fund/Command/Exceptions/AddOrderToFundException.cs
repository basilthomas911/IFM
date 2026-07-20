namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs while attempting to remove a trade from a fund order.
/// </summary>
public class RemoveTradeFromFundOrderException(string errorMessage)
    : ApplicationException(errorMessage)
{
}
