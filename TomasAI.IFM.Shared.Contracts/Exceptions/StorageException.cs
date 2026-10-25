namespace TomasAI.IFM.Shared.Exceptions;

/// <summary>
/// Represents errors that occur during storage operations.
/// </summary>
/// <remarks>Use this exception to indicate failures related to storage access, such as read or write errors,
/// unavailable storage resources, or invalid storage operations. This exception is typically thrown by storage-related
/// components to provide additional context about the underlying error.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
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
