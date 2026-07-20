namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;

/// <summary>
/// Represents errors that occur when attempting to remove an economic calendar entry.
/// </summary>
/// <remarks>This exception is typically thrown when a failure occurs during the removal process of an economic
/// calendar item, such as when the item does not exist or cannot be deleted due to system constraints. Use this
/// exception to handle and diagnose issues specific to economic calendar removal operations.</remarks>
public class RemoveEconomicCalendarException : ApplicationException
{
    public RemoveEconomicCalendarException(string errorMessage) : base(errorMessage)
    {
    }

    public RemoveEconomicCalendarException() : base()
    {
    }

    public RemoveEconomicCalendarException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
