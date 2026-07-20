namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents errors that occur when closing a fund order fails.
/// </summary>
/// <remarks>This exception is typically thrown to indicate that an operation to close a fund order could not be
/// completed successfully. Use the exception message to obtain details about the specific failure. This exception may
/// wrap an inner exception that provides additional context about the underlying cause.</remarks>
public class CloseFundOrderException : ApplicationException
{
    public CloseFundOrderException(string errorMessage) : base(errorMessage)
    {
    }
   

    public CloseFundOrderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CloseFundOrderException() : base()
    {
    }
}
