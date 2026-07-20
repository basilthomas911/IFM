namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;

/// <summary>
/// Represents errors that occur when a change lookup operation encounters an invalid or unsupported type.
/// </summary>
/// <remarks>This exception is typically thrown when an operation attempts to perform a lookup or conversion
/// involving a type that is not permitted or recognized by the change tracking system. Catch this exception to handle
/// scenarios where type mismatches or unsupported types are encountered during change lookup processes.</remarks>
public class ChangeLookupTypeException :ApplicationException
{
    public ChangeLookupTypeException() : base()
    {
    }

    public ChangeLookupTypeException(string message) : base(message)
    {
    }

    public ChangeLookupTypeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
