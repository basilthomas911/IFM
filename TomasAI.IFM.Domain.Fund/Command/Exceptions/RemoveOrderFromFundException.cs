namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur when attempting to remove an order from a fund.
/// </summary>
/// <remarks>This exception is typically thrown when a business rule or validation prevents the removal of an
/// order from a fund. Catch this exception to handle scenarios where such removal operations are not permitted or fail
/// due to domain-specific constraints.</remarks>
public class RemoveOrderFromFundException : ApplicationException
{
    public RemoveOrderFromFundException(string errorMessage) : base(errorMessage)
    {
    }

    public RemoveOrderFromFundException() : base()
    {
    }

    public RemoveOrderFromFundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
