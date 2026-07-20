namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Exceptions;

/// <summary>
/// Represents errors that occur when adding an economic calendar entry fails.
/// </summary>
/// <remarks>Use this exception to indicate failures specific to adding economic calendar data, such as validation
/// errors or data conflicts. This exception is intended for application-level error handling and can be caught to
/// provide user-friendly feedback or logging.</remarks>
public class AddEconomicCalendarException : ApplicationException
{
    public AddEconomicCalendarException(string errorMessage) : base(errorMessage)
    {
    }

    public AddEconomicCalendarException() : base()
    {
    }

    public AddEconomicCalendarException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
