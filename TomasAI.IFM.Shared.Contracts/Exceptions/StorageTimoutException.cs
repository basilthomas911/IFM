namespace TomasAI.IFM.Shared.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a storage operation exceeds the allowed timeout period.
/// </summary>
/// <remarks>This exception typically indicates that a storage request did not complete within the configured
/// timeout interval. It can be used to distinguish timeout-related failures from other storage errors. Catch this
/// exception to implement retry logic or to provide user feedback about operation delays.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
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
