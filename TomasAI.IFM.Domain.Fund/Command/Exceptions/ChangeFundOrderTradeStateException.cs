namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur when changing the trade state of a fund order fails.
/// </summary>
/// <remarks>This exception is typically thrown by operations that attempt to update the trade state of a fund
/// order but encounter a failure due to business rules, invalid state transitions, or other domain-specific
/// constraints. Catch this exception to handle scenarios where a fund order's trade state cannot be changed as
/// requested.</remarks>
public class ChangeFundOrderTradeStateException : ApplicationException
{
    public ChangeFundOrderTradeStateException() : base()
    {
    }

    public ChangeFundOrderTradeStateException(string errorMessage) : base(errorMessage)
    {
    }

    public ChangeFundOrderTradeStateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
