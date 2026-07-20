namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur when adding a trade to a fund order fails.
/// </summary>
/// <remarks>This exception is typically thrown to indicate that an operation to add a trade to a fund order could
/// not be completed due to a business or validation error. Catch this exception to handle such failures specifically in
/// fund order processing workflows.</remarks>
public class AddTradeToFundOrderException : ApplicationException
{
    public AddTradeToFundOrderException(string errorMessage) : base(errorMessage)
    {
    }

    public AddTradeToFundOrderException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
