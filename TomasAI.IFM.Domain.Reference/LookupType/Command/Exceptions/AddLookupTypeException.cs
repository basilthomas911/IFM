namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;

/// <summary>
/// Represents errors that occur when adding a lookup type fails during application execution.
/// </summary>
/// <remarks>This exception is typically thrown when an attempt to add a new lookup type to a collection or
/// registry is unsuccessful due to invalid input, conflicts, or other application-specific constraints. Catch this
/// exception to handle lookup type addition failures gracefully in your application logic.</remarks>
public class AddLookupTypeException : ApplicationException
{
    public AddLookupTypeException() : base()
    {
    }

    public AddLookupTypeException(string message) : base(message)
    {
    }

    public AddLookupTypeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
