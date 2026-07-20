namespace TomasAI.IFM.Domain.Fund.Transaction.Command.Exceptions;

/// <summary>
/// Exception thrown when processing end-of-day fund transactions fails within the fund transaction actor.
/// </summary>
/// <remarks>
/// Use this exception to provide a domain-specific error type for failures that occur while running
/// end-of-day processing for fund transactions (for example during settlement, PnL aggregation or cleanup tasks).
/// </remarks>
public class ProcessEndOfDayFundTransactionException : ApplicationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEndOfDayFundTransactionException"/> class.
    /// </summary>
    public ProcessEndOfDayFundTransactionException() : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEndOfDayFundTransactionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ProcessEndOfDayFundTransactionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessEndOfDayFundTransactionException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ProcessEndOfDayFundTransactionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
