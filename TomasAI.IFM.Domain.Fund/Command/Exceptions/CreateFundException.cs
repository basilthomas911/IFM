namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur during the creation of a fund.
/// </summary>
/// <remarks>This exception is typically thrown when a fund creation operation fails due to invalid input,
/// business rule violations, or other application-specific errors. Catch this exception to handle fund creation
/// failures in a controlled manner.</remarks>
public class CreateFundException : ApplicationException
{
    public CreateFundException(string errorMessage):base(errorMessage)
    {
    }

    public CreateFundException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }
}
