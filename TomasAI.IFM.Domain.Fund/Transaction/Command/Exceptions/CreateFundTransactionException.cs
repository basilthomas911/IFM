namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;

/// <summary>
/// Exception thrown when creating a fund transaction fails within the fund transaction actor.
/// </summary>
/// <remarks>
/// This exception can be used by command handlers and decorators in the fund transaction actor
/// to provide a domain-specific error type for failures during transaction creation.
/// </remarks>
public class CreateFundTransactionException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionException"/> class.
    /// </summary>
    public CreateFundTransactionException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CreateFundTransactionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFundTransactionException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CreateFundTransactionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
