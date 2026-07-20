namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;

/// <summary>
/// Exception thrown when creating multiple fund transactions fails within the fund transaction actor.
/// </summary>
/// <remarks>
/// Use this exception to provide a domain-specific error type for failures that occur while processing
/// batch fund transaction creation commands in the fund transaction actor.
/// </remarks>
public class CreateFundTransactionsException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionsException"/> class.
    /// </summary>
    public CreateFundTransactionsException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionsException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CreateFundTransactionsException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionsException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CreateFundTransactionsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
