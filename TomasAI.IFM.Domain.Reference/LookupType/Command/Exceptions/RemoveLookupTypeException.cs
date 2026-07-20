namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Exceptions;

/// <summary>
/// 
/// </summary>
public class RemoveLookupTypeException :ApplicationException
{
    public RemoveLookupTypeException() : base()
    {
    }

    public RemoveLookupTypeException(string message) : base(message)
    {
    }

    public RemoveLookupTypeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
