namespace TomasAI.IFM.Shared.Exceptions;

/// <summary>
/// Represents errors that occur during storage operations.
/// </summary>
/// <remarks>Use this exception to indicate failures related to storage access, such as read or write errors,
/// unavailable storage resources, or invalid storage operations. This exception is typically thrown by storage-related
/// components to provide additional context about the underlying error.</remarks>
public class StorageException : ApplicationException
{
    public StorageException(string errorMessage) : base(errorMessage)
    {
    }

    public StorageException(string errorMessage, Exception innerException) : base(errorMessage, innerException)
    {
    }

    public StorageException() : base()
    {
    }
}

/// <summary>
/// Represents an exception that is thrown when a storage operation exceeds the allowed timeout period.
/// </summary>
/// <remarks>This exception typically indicates that a storage request did not complete within the configured
/// timeout interval. It can be used to distinguish timeout-related failures from other storage errors. Catch this
/// exception to implement retry logic or to provide user feedback about operation delays.</remarks>
public class StorageTimoutException : ApplicationException
{
    public StorageTimoutException(string errorMessage) : base(errorMessage)
    {
    }

    public StorageTimoutException(string errorMessage, Exception innerException) : base(errorMessage, innerException)
    {
    }

    public StorageTimoutException() : base()
    {
    }
}
